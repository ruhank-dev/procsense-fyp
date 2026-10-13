"""Alembic against a real PostgreSQL: the migrations run, undo and match the models."""
from pathlib import Path

import pytest
from alembic import command
from alembic.config import Config
from alembic.script import ScriptDirectory
from sqlalchemy import text

SERVER = Path(__file__).resolve().parents[1]


def alembic_config(url: str) -> Config:
    cfg = Config(str(SERVER / "alembic.ini"))
    cfg.set_main_option("sqlalchemy.url", url.replace("%", "%%"))
    return cfg


def test_there_is_exactly_one_head():
    script = ScriptDirectory.from_config(alembic_config("postgresql+psycopg://u:p@127.0.0.1:1/none"))
    assert len(script.get_heads()) == 1


@pytest.mark.db
def test_upgrade_to_head_and_back_to_nothing(clean_database, db_url):
    cfg = alembic_config(db_url)
    command.upgrade(cfg, "head")
    with clean_database.connect() as conn:
        head = ScriptDirectory.from_config(cfg).get_current_head()
        assert conn.execute(text("select version_num from alembic_version")).scalar_one() == head
    command.downgrade(cfg, "base")
    with clean_database.connect() as conn:
        assert conn.execute(text("select count(*) from alembic_version")).scalar_one() == 0


@pytest.mark.db
def test_the_models_and_the_migrations_agree(clean_database, db_url):
    cfg = alembic_config(db_url)
    command.upgrade(cfg, "head")
    command.check(cfg)   # raises if a model has a change that no migration makes


@pytest.mark.db
def test_upgrading_twice_changes_nothing(clean_database, db_url):
    cfg = alembic_config(db_url)
    command.upgrade(cfg, "head")
    command.upgrade(cfg, "head")
    with clean_database.connect() as conn:
        assert conn.execute(text("select count(*) from alembic_version")).scalar_one() == 1
