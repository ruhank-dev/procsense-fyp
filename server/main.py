"""The ProcSense service. This file only creates the app and includes the routers (INSTRUCTIONS §4b)."""
from __future__ import annotations

import logging
import threading
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.staticfiles import StaticFiles

from server import database, logging_setup
from server.config import get_settings
from server.middleware.audit import AuditMiddleware, get_writer
from server.middleware.security_headers import SecurityHeaders
from server.routers import ai_audit, ai_gateway, asr, classify, debrief, dialogue, orders, tts, dashboard, device_auth, export, health, internal, me, organisations, pages, privacy, scenario_packages, sessions


def _sweep_on_start() -> None:
    """When the service wakes (the free host sleeps), anything left pending is picked up in the background."""
    from server.verification.worker import get_runner, verify_pending
    if get_runner() is None:
        return
    try:
        verify_pending(get_settings().verify_batch)
    except Exception:
        logging.getLogger("procsense.verification").exception("the sweep at start failed")


@asynccontextmanager
async def lifespan(app: FastAPI):
    threading.Thread(target=_sweep_on_start, name="verify-sweep", daemon=True).start()
    yield
    get_writer().flush(5)   # the AI audit rows still waiting are written before the connection closes
    engine = database.get_engine()
    if engine is not None:
        engine.dispose()


def create_app() -> FastAPI:
    logging_setup.install()
    app = FastAPI(title="ProcSense service", version="0.1.0", lifespan=lifespan)
    app.include_router(health.router)
    app.include_router(me.router)
    app.include_router(device_auth.router)
    app.include_router(privacy.router)
    app.include_router(organisations.router)
    app.include_router(sessions.router)
    app.include_router(dashboard.router)
    app.include_router(export.router)
    app.include_router(ai_audit.router)
    app.include_router(ai_gateway.router)
    app.include_router(ai_gateway.health_router)
    app.include_router(asr.router)
    app.include_router(tts.router)
    app.include_router(classify.router)
    app.include_router(orders.router)
    app.include_router(dialogue.router)
    app.include_router(debrief.router)
    app.include_router(scenario_packages.router)
    app.include_router(internal.router)
    app.include_router(pages.router)
    app.mount("/static", StaticFiles(directory=pages.STATIC), name="static")
    app.add_middleware(AuditMiddleware)
    app.add_middleware(SecurityHeaders)
    return app


app = create_app()
