"""The ProcSense service. This file only creates the app and includes the routers (INSTRUCTIONS §4b)."""
from __future__ import annotations

import logging
import threading
from contextlib import asynccontextmanager

from fastapi import FastAPI

from server import database
from server.config import get_settings
from server.routers import dashboard, device_auth, export, health, internal, me, organisations, scenario_packages, sessions


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
    engine = database.get_engine()
    if engine is not None:
        engine.dispose()


def create_app() -> FastAPI:
    app = FastAPI(title="ProcSense service", version="0.1.0", lifespan=lifespan)
    app.include_router(health.router)
    app.include_router(me.router)
    app.include_router(device_auth.router)
    app.include_router(organisations.router)
    app.include_router(sessions.router)
    app.include_router(dashboard.router)
    app.include_router(export.router)
    app.include_router(scenario_packages.router)
    app.include_router(internal.router)
    return app


app = create_app()
