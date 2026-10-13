"""Liveness and database health. Routers stay thin: they call the repository and shape the reply."""
from __future__ import annotations

from collections.abc import Iterator

from fastapi import APIRouter, Depends, Response
from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.orm import Session

from server.config import Settings, get_settings
from server.database import get_session
from server.repositories import health as repository
from server.schemas.health import DbHealth, Health

router = APIRouter(tags=["health"])


def _session_or_none() -> Iterator[Session | None]:
    try:
        yield from get_session()
    except RuntimeError:
        yield None


@router.get("/health", response_model=Health)
def health(settings: Settings = Depends(get_settings)) -> Health:
    """The service is up. Does not touch the database (a sleeping or paused database must not make the service look dead)."""
    return Health(status="ok", service="procsense-server", app_env=settings.app_env, git_sha=settings.git_sha)


@router.get("/health/db", response_model=DbHealth)
def health_db(response: Response, session: Session | None = Depends(_session_or_none), settings: Settings = Depends(get_settings)) -> DbHealth:
    """A database round trip, with the versions the service accepts. A failure is a 503 with a short reason and never the address or the error text."""
    if session is None:
        response.status_code = 503
        return DbHealth(status="unavailable", reason="database_not_configured")
    try:
        result = repository.ping(session)
    except SQLAlchemyError:
        response.status_code = 503
        return DbHealth(status="unavailable", reason="database_unreachable")
    return DbHealth(status="ok", latency_ms=result.latency_ms, server_version=result.server_version,
                    supported_engine_versions=settings.supported_engine_versions, supported_content_versions=settings.supported_content_versions)
