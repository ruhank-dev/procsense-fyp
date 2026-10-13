"""The database connection: SQLAlchemy 2 engine and session for PostgreSQL (INSTRUCTIONS §5b: ORM only)."""
from __future__ import annotations

from collections.abc import Iterator
from functools import lru_cache

from sqlalchemy import MetaData, create_engine
from sqlalchemy.engine import Engine
from sqlalchemy.orm import DeclarativeBase, Session
from sqlalchemy.pool import NullPool

from server.config import get_settings

# constraint names that never change between databases, so migrations stay readable and `alembic check` is stable
NAMING_CONVENTION = {
    "ix": "ix_%(column_0_label)s",
    "uq": "uq_%(table_name)s_%(column_0_name)s",
    "ck": "ck_%(table_name)s_%(constraint_name)s",
    "fk": "fk_%(table_name)s_%(column_0_name)s_%(referred_table_name)s",
    "pk": "pk_%(table_name)s",
}


class Base(DeclarativeBase):
    metadata = MetaData(naming_convention=NAMING_CONVENTION)


def make_engine(url: str, pool_size: int = 3, connect_timeout: int = 10) -> Engine:
    """An engine for the application. Prepared statements are off because Supabase's transaction pooler (port 6543) does not support them; the pool is small because the free plan allows few connections.
    pool_size 0 means no pool at all (one connection per use). The connect timeout stops a paused or unreachable database from holding a request for minutes."""
    options: dict = {"connect_args": {"prepare_threshold": None, "connect_timeout": connect_timeout}, "pool_pre_ping": True}
    if pool_size <= 0:
        options["poolclass"] = NullPool
    else:
        options.update(pool_size=pool_size, max_overflow=1, pool_recycle=300)
    return create_engine(url, **options)


@lru_cache
def get_engine() -> Engine | None:
    """The application's engine, made on first use; None when no database address is configured (the service still starts, /health/db says so)."""
    settings = get_settings()
    url = settings.database_url.get_secret_value()
    return make_engine(url, settings.db_pool_size, settings.db_connect_timeout) if url else None


def get_session() -> Iterator[Session]:
    """A session per request, always closed."""
    engine = get_engine()
    if engine is None:
        raise RuntimeError("database_not_configured")
    with Session(engine) as session:
        yield session
