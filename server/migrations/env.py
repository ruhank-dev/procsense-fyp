"""Alembic environment. The database address comes from the settings (MIGRATION_DATABASE_URL), or from a test that sets sqlalchemy.url on the configuration; it is never in a file."""
from __future__ import annotations

from alembic import context
from sqlalchemy import create_engine, pool

from server import models  # noqa: F401  (importing the models registers their tables on Base.metadata)
from server.config import get_settings
from server.database import Base

config = context.config
target_metadata = Base.metadata


def _url() -> str:
    url = config.get_main_option("sqlalchemy.url") or get_settings().migration_database_url.get_secret_value()
    if not url:
        raise RuntimeError("no migration database address: set MIGRATION_DATABASE_URL (the session pooler, port 5432) in server/.env or the environment")
    return url


def run_migrations_offline() -> None:
    context.configure(url=_url(), target_metadata=target_metadata, literal_binds=True, dialect_opts={"paramstyle": "named"}, compare_type=True)
    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online() -> None:
    # NullPool: a migration is one short connection; prepared statements off, as for the application
    engine = create_engine(_url(), poolclass=pool.NullPool, connect_args={"prepare_threshold": None, "connect_timeout": 15})
    with engine.connect() as connection:
        context.configure(connection=connection, target_metadata=target_metadata, compare_type=True)
        with context.begin_transaction():
            context.run_migrations()


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
