"""Generated scenario packages, stored once by the SHA-256 of their bytes (p1-backend-s02).

A session of a generated scenario names its package in the header; the server needs the package to replay the session. The headset sends it here when the upload answers `package_needed`.
The hash in the address must be the hash of the bytes sent, so nobody can store one thing under another's name, and a package is stored once however many sessions use it.
"""
from __future__ import annotations

import hashlib
import json
import re

from pathlib import Path

from fastapi import APIRouter, Depends, HTTPException, Request, Response
from sqlalchemy.orm import Session as DbSession

from server.auth.dependencies import CurrentUser, current_user, db_session
from server.config import Settings, get_settings
from server.generation import history as generation_history
from server.repositories import generation_history as history_table
from server.repositories import sessions as repository
from server.schemas.sessions import PackageOffer, PackageReply, PackageRequest

router = APIRouter(tags=["scenario packages"])
HASH = re.compile(r"^[0-9a-f]{64}$")


def _active_member(user: CurrentUser) -> None:
    if not any(m.status == "active" for m in user.memberships) and not user.profile.is_platform_admin:
        raise HTTPException(status_code=403, detail="no_active_membership")


@router.post("/scenario-packages/next", response_model=PackageOffer)
def next_package(body: PackageRequest, user: CurrentUser = Depends(current_user), db: DbSession = Depends(db_session), settings: Settings = Depends(get_settings)) -> PackageOffer:
    """The next pre-generated package for a family and level that the trainee has not met, checked against the pool's index and stored here by its hash (DT-199). The headset then fetches the bytes with GET /scenario-packages/{hash}
    and plays them in practice mode; an assessment session refuses a generated package whatever is fetched. The pool is a folder the operator fills with tools/generation/pool.py; without one the answer is 503 no_pool, and with no package left 404 pool_empty:
    the headset then plays an authored case, as it does offline."""
    _active_member(user)
    if not settings.package_pool_dir:
        raise HTTPException(status_code=503, detail={"code": "no_pool"})
    root = Path(settings.package_pool_dir)
    try:
        index = json.loads((root / "index.json").read_text(encoding="utf-8"))
    except (OSError, ValueError):
        raise HTTPException(status_code=503, detail={"code": "no_pool"}) from None
    for e in index.get("packages", []):
        if e["scenario"] != body.scenario or e["level"] != body.level or e["spine"] in body.exclude_spines or e["fingerprint"] in body.exclude_fingerprints:
            continue
        path = root / e["file"]
        try:
            raw = path.read_bytes().replace(b"\r\n", b"\n")
        except OSError:
            continue
        digest = hashlib.sha256(raw).hexdigest()
        if digest != e["sha256"] or len(raw) > settings.max_package_bytes:
            continue                                                                  # a damaged or changed file is never served
        try:
            pkg = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, ValueError):
            continue
        repository.store_package(db, digest, raw)
        db.commit()
        return PackageOffer(package_hash=digest, scenario=body.scenario, level=body.level, case_id=pkg["case"]["id"], spine=e["spine"])
    raise HTTPException(status_code=404, detail={"code": "pool_empty"})


_CONTEXT: list = []


def _context():
    """What the pipeline reads and never changes, built once per process (the index takes a moment)."""
    if not _CONTEXT:
        from server.generation import objectives as ob, pipeline, retrieve, skeleton
        B, _ = skeleton._composer()
        _CONTEXT.append(pipeline.Context(retrieve.build_index(), B.load_cases(), ob.Tables.load()))
    return _CONTEXT[0]


@router.post("/scenario-packages/generate", response_model=PackageOffer)
def generate_package(body: PackageRequest, user: CurrentUser = Depends(current_user), db: DbSession = Depends(db_session), settings: Settings = Depends(get_settings)) -> PackageOffer:
    """A package built on request (DT-199, p1-live-scenario-generation): the person's own history (DT-201) steers the objectives and the novelty check, the deterministic pipeline builds and gates a package, and it is stored by its hash
    like a pooled one. Practice only. Answers 503 generation_off unless LIVE_GENERATION is on, and 404 pool_fallback when the pipeline could not make a good package (the headset then asks for a pooled one, or plays an authored case)."""
    _active_member(user)
    if not settings.live_generation:
        raise HTTPException(status_code=503, detail={"code": "generation_off"})
    from server.generation import pipeline
    entries = history_table.entries(db, user.profile.user_id, user.profile.pseudonym)
    fingerprints = generation_history.recent_fingerprints(entries) + list(body.exclude_fingerprints)
    request = {"version": 1, "trainee": user.profile.pseudonym, "mode": "practice", "scenario": body.scenario, "level": body.level}
    result = pipeline.generate(request, generation_history.session_records(entries), generation_history.novelty_history(entries), _context(), recent_fingerprints=fingerprints)
    if result.package is None:
        raise HTTPException(status_code=404, detail={"code": "pool_fallback", "fallback": result.fallback})
    from server.generation import package as package_module
    raw = package_module.file_text(result.package).encode("utf-8")
    if len(raw) > settings.max_package_bytes:
        raise HTTPException(status_code=413, detail={"code": "package_too_large"})
    digest = hashlib.sha256(raw).hexdigest()
    repository.store_package(db, digest, raw)
    db.commit()
    return PackageOffer(package_hash=digest, scenario=body.scenario, level=body.level, case_id=result.package["case"]["id"], spine=result.package["provenance"]["spine"])


@router.get("/scenario-packages/{package_hash}")
def get_package(package_hash: str, user: CurrentUser = Depends(current_user), db: DbSession = Depends(db_session)) -> Response:
    """The stored bytes of a package, for an active member: exactly the bytes whose SHA-256 is the address, so the headset can check them before playing."""
    if not HASH.match(package_hash):
        raise HTTPException(status_code=422, detail={"code": "BAD_HASH"})
    _active_member(user)
    raw = repository.get_package(db, package_hash)
    if raw is None:
        raise HTTPException(status_code=404, detail={"code": "no_such_package"})
    return Response(content=raw, media_type="application/json", headers={"ETag": f'"{package_hash}"', "Cache-Control": "private, max-age=31536000, immutable"})


@router.put("/scenario-packages/{package_hash}", response_model=PackageReply, status_code=201)
async def put_package(package_hash: str, request: Request, response: Response, user: CurrentUser = Depends(current_user), db: DbSession = Depends(db_session),
                      settings: Settings = Depends(get_settings)) -> PackageReply:
    if not HASH.match(package_hash):
        raise HTTPException(status_code=422, detail={"code": "BAD_HASH"})
    if not any(m.status == "active" for m in user.memberships) and not user.profile.is_platform_admin:
        raise HTTPException(status_code=403, detail="no_active_membership")
    raw = b""
    async for chunk in request.stream():
        raw += chunk
        if len(raw) > settings.max_package_bytes:
            raise HTTPException(status_code=413, detail="too_large")
    if hashlib.sha256(raw).hexdigest() != package_hash:
        raise HTTPException(status_code=422, detail={"code": "HASH_MISMATCH"})
    try:
        if not isinstance(json.loads(raw.decode("utf-8")), dict):
            raise ValueError
    except (UnicodeDecodeError, ValueError):
        raise HTTPException(status_code=422, detail={"code": "NOT_A_JSON_OBJECT"}) from None
    if not repository.store_package(db, package_hash, raw):
        response.status_code = 200
        return PackageReply(package_hash=package_hash, outcome="already_stored")
    db.commit()
    return PackageReply(package_hash=package_hash, outcome="stored")
