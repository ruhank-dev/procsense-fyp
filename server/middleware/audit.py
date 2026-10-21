"""The AI audit logger (p1-backend-s05): one `ai_call` row for every AI request, written in the background so the reply never waits for the database.

How an AI route uses it (the gateway routes of p1-backend-s06):

    @router.post("/evaluate/organisations/{organisation_id}")
    def evaluate(body: EvaluateIn, call: AiCallRecord = Depends(audited("evaluate"))):
        call.set_model("qwen2.5-7b-instruct", "q4_K_M")
        call.set_prompt("gemma-eval-3", context_id=body.case_id)
        call.set_input(body.utterance)            # only its hash is kept
        call.session_id = body.session_id
        ...
        call.validated(ok, reasons)               # codes such as UNSUPPORTED_CLAIM
        call.set_output(ref=line_id)              # or set_output(text=...) - only its hash is kept
        call.fell_back("MODEL_TIMEOUT")           # when the deterministic line was used

`audited(task)` signs the person in (any member of the organisation in the address) and starts the record; the middleware times the request and, when it is over (an answer, an error or a crash),
finishes the record with the status and the latency and hands it to the writer. The reply carries `X-AI-Call-Id`, so the headset can write the same id into its own session log (the mirror);
a crash is answered by the framework's outermost error handler, so that one reply has no id, but its row (status 500, validation `error`) is still written.

T-AU1 (completeness): every request that started a record writes exactly one row, whatever happened to it; `unaudited_ai_routes(app)` lists any AI route that does not start one (a test keeps it empty).
T-AU2 (no raw text): the record holds no text - hashes, references and codes only (a reason that is not a code is replaced by UNCODED_REASON).
T-AU3 (overhead under 5 ms): the request only fills a small object and puts it on a queue; a background thread writes the rows in batches through the pooled connection.
"""
from __future__ import annotations

import hashlib
import logging
import queue
import re
import threading
import time
import uuid
from dataclasses import dataclass, field
from functools import lru_cache

from fastapi import Depends, FastAPI, Request
from fastapi.routing import APIRoute
from sqlalchemy import insert
from starlette.datastructures import MutableHeaders
from starlette.types import ASGIApp, Message, Receive, Scope, Send

from server.auth.dependencies import OrgContext, require_member
from server.config import get_settings
from server.database import get_engine
from server.models.ai_call import AiCall

log = logging.getLogger("procsense.audit")

# the addresses of the AI capabilities (p1-backend-s06); a route under one of them must start an audit record
AI_PREFIXES = ("/llm", "/dialogue", "/behaviour", "/evaluate", "/debrief", "/asr", "/tts", "/orders")
SCOPE_KEY = "procsense.ai_call"
HEADER = "x-ai-call-id"
CODE = re.compile(r"^[A-Z][A-Z0-9_]{0,39}$")
REF = re.compile(r"^[A-Za-z0-9:._/+-]{0,80}$")
MAX_REASONS = 20


def hash_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def _ref(value: str, limit: int) -> str:
    """A name, an id or a reference; anything that looks like text (spaces, punctuation) is not stored, its hash is."""
    value = value or ""
    if len(value) <= limit and REF.match(value):
        return value
    return ("sha256:" + hash_text(value))[:limit]


def _code(value: str) -> str:
    return value if CODE.match(value or "") else "UNCODED_REASON"


@dataclass
class AiCallRecord:
    organisation_id: uuid.UUID
    user_id: uuid.UUID
    task: str
    route: str
    id: uuid.UUID = field(default_factory=uuid.uuid4)
    session_id: uuid.UUID | None = None
    model: str = ""
    model_version: str = ""
    prompt_id: str = ""
    context_id: str = ""
    input_hash: str = ""
    output_ref: str = ""
    validation: str = "not_reported"
    reasons: list[str] = field(default_factory=list)
    fallback: bool = False
    fallback_reason: str = ""

    def set_model(self, name: str, version: str = "") -> None:
        self.model, self.model_version = _ref(name, 64), _ref(version, 64)

    def set_prompt(self, prompt_id: str, context_id: str = "") -> None:
        self.prompt_id, self.context_id = _ref(prompt_id, 64), _ref(context_id, 64)

    def set_input(self, text: str | bytes) -> None:
        self.input_hash = hashlib.sha256(text if isinstance(text, bytes) else text.encode("utf-8")).hexdigest()

    def set_output(self, *, text: str | bytes | None = None, ref: str | None = None) -> None:
        """A reference to what was said: the id of a pre-approved line, or the hash of generated text (the text itself is in the headset's log only)."""
        if ref is not None:
            self.output_ref = _ref(ref, 80)
        elif text is not None:
            self.output_ref = "sha256:" + hashlib.sha256(text if isinstance(text, bytes) else text.encode("utf-8")).hexdigest()

    def validated(self, passed: bool, reasons: list[str] | tuple[str, ...] = ()) -> None:
        self.validation = "passed" if passed else "failed"
        self.reasons = [_code(r) for r in list(reasons)[:MAX_REASONS]]

    def validation_skipped(self) -> None:
        self.validation = "skipped"

    def fell_back(self, reason: str) -> None:
        self.fallback, self.fallback_reason = True, _code(reason)

    def row(self, status_code: int, latency_ms: int) -> dict:
        validation = self.validation
        if validation == "not_reported" and status_code >= 500:
            validation = "error"
        return dict(id=self.id, organisation_id=self.organisation_id, user_id=self.user_id, session_id=self.session_id, task=self.task[:32], route=self.route[:64], model=self.model,
                    model_version=self.model_version, prompt_id=self.prompt_id, context_id=self.context_id, input_hash=self.input_hash, output_ref=self.output_ref, validation=validation,
                    reasons=self.reasons, fallback=self.fallback, fallback_reason=self.fallback_reason, status_code=status_code, latency_ms=max(0, latency_ms))


def begin(request: Request, task: str, organisation_id: uuid.UUID, user_id: uuid.UUID, session_id: uuid.UUID | None = None) -> AiCallRecord:
    """Starts the audit record of this request. The middleware finishes and writes it."""
    holder = request.scope.get(SCOPE_KEY)
    if holder is None:
        raise RuntimeError("the AI audit middleware is not installed (server.main adds AuditMiddleware)")
    if holder.get("record") is not None:
        raise RuntimeError("this request already has an AI audit record")
    record = AiCallRecord(organisation_id=organisation_id, user_id=user_id, task=task, route=request.scope.get("path", ""), session_id=session_id)
    holder["record"] = record
    return record


def audited(task: str):
    """The dependency every AI route takes: a signed-in member of the organisation in the address, and the started audit record."""
    def dependency(request: Request, ctx: OrgContext = Depends(require_member)) -> AiCallRecord:
        return begin(request, task, ctx.organisation.id, ctx.user.profile.user_id)
    dependency.audited_task = task
    return dependency


def _uses_audited(dependant) -> bool:
    return any(getattr(d.call, "audited_task", None) or _uses_audited(d) for d in dependant.dependencies)


def api_routes(routes, prefix: str = "", inherited: bool = False):
    """Every API route of the app with its full path and whether a router above it already takes `audited`. Walks included routers too (this FastAPI keeps them as wrapped routers in app.routes)."""
    for r in routes:
        if isinstance(r, APIRoute):
            yield prefix + r.path, r, inherited
        elif getattr(r, "original_router", None) is not None:
            context = getattr(r, "include_context", None)
            audited_here = any(getattr(getattr(d, "dependency", None), "audited_task", None) for d in getattr(context, "dependencies", []))
            yield from api_routes(r.original_router.routes, prefix + getattr(context, "prefix", ""), inherited or audited_here)


def unaudited_ai_routes(app: FastAPI) -> list[str]:
    """T-AU1: the AI routes that do not start an audit record (should be none)."""
    return sorted(f"{','.join(sorted(r.methods))} {path}" for path, r, inherited in api_routes(app.routes)
                  if path.startswith(AI_PREFIXES) and not inherited and not _uses_audited(r.dependant))


# ---------------------------------------------------------------- the writer

class AuditWriter:
    """Writes the rows from a background thread, in batches, through the application's pooled connection. A full queue drops the row (and says so in the log) rather than hold a reply up."""

    def __init__(self, size: int, batch: int = 200, interval: float = 0.5, engine_factory=get_engine):
        self._q: queue.Queue[dict] = queue.Queue(maxsize=size)
        self._batch, self._interval, self._engine_factory = batch, interval, engine_factory
        self._thread: threading.Thread | None = None
        self._lock = threading.Lock()
        self.written = self.dropped = self.failed = 0

    def submit(self, row: dict) -> bool:
        try:
            self._q.put_nowait(row)
        except queue.Full:
            self.dropped += 1
            log.error("AI audit queue full: row %s dropped (%s dropped so far)", row["id"], self.dropped)
            return False
        self._start()
        return True

    def _start(self) -> None:
        if self._thread is None or not self._thread.is_alive():
            with self._lock:
                if self._thread is None or not self._thread.is_alive():
                    self._thread = threading.Thread(target=self._run, name="ai-audit-writer", daemon=True)
                    self._thread.start()

    def _run(self) -> None:
        while True:
            rows = [self._q.get()]
            deadline = time.monotonic() + self._interval
            while len(rows) < self._batch:
                try:
                    rows.append(self._q.get(timeout=max(0.0, deadline - time.monotonic())))
                except queue.Empty:
                    break
            try:
                self._write(rows)
            finally:
                for _ in rows:
                    self._q.task_done()

    def _write(self, rows: list[dict]) -> None:
        engine = self._engine_factory()
        if engine is None:
            self.failed += len(rows)
            log.warning("no database configured: %s AI audit rows not written", len(rows))
            return
        for attempt in range(3):
            try:
                with engine.begin() as conn:
                    conn.execute(insert(AiCall), rows)
                self.written += len(rows)
                return
            except Exception:
                log.exception("writing %s AI audit rows failed (attempt %s of 3)", len(rows), attempt + 1)
                time.sleep(0.2 * (attempt + 1))
        self.failed += len(rows)
        log.error("%s AI audit rows lost after 3 attempts: %s", len(rows), ", ".join(str(r["id"]) for r in rows))

    def flush(self, timeout: float = 5.0) -> bool:
        """Waits until every submitted row is written (at shut-down, and in tests). Returns False on timeout."""
        end = time.monotonic() + timeout
        while self._q.unfinished_tasks:
            if time.monotonic() > end:
                return False
            time.sleep(0.01)
        return True


@lru_cache
def get_writer() -> AuditWriter:
    return AuditWriter(get_settings().audit_queue_size)


# ---------------------------------------------------------------- the middleware

class AuditMiddleware:
    def __init__(self, app: ASGIApp):
        self.app = app

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return
        started = time.perf_counter()
        holder: dict = {"record": None}
        scope[SCOPE_KEY] = holder
        status = {"code": 500}

        async def send_with_id(message: Message) -> None:
            if message["type"] == "http.response.start":
                status["code"] = message["status"]
                record = holder["record"]
                if record is not None:
                    MutableHeaders(scope=message)[HEADER] = str(record.id)
            await send(message)

        try:
            await self.app(scope, receive, send_with_id)
        except Exception:
            status["code"] = 500
            raise
        finally:
            record = holder["record"]
            if record is not None:
                get_writer().submit(record.row(status["code"], int((time.perf_counter() - started) * 1000)))
            elif scope.get("path", "").startswith(AI_PREFIXES) and status["code"] < 400:
                log.error("AI route %s answered %s without an audit record", scope.get("path"), status["code"])
