"""The ProcSense service. This file only creates the app and includes the routers (INSTRUCTIONS §4b)."""
from __future__ import annotations

from contextlib import asynccontextmanager

from fastapi import FastAPI

from server import database
from server.routers import health, me, organisations


@asynccontextmanager
async def lifespan(app: FastAPI):
    yield
    engine = database.get_engine()
    if engine is not None:
        engine.dispose()


def create_app() -> FastAPI:
    app = FastAPI(title="ProcSense service", version="0.1.0", lifespan=lifespan)
    app.include_router(health.router)
    app.include_router(me.router)
    app.include_router(organisations.router)
    return app


app = create_app()
