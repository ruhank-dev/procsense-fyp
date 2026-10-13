"""Settings (no database needed)."""
import pytest
from pydantic import ValidationError

from server.config import Settings


def make(**env) -> Settings:
    return Settings(_env_file=None, **env)


def test_defaults_start_without_any_configuration():
    s = make()
    assert s.app_env == "dev"
    assert s.database_url.get_secret_value() == ""
    assert s.db_pool_size == 3
    assert s.supported_engine_versions == []


def test_a_supabase_address_is_made_to_use_the_psycopg_driver():
    s = make(database_url="postgresql://postgres.ref:pw@pooler.example.com:6543/postgres")
    assert s.database_url.get_secret_value().startswith("postgresql+psycopg://")
    s = make(database_url="postgres://postgres.ref:pw@pooler.example.com:6543/postgres")
    assert s.database_url.get_secret_value().startswith("postgresql+psycopg://")


def test_a_database_that_is_not_postgresql_is_refused():
    with pytest.raises(ValidationError):
        make(database_url="sqlite:///local.db")
    with pytest.raises(ValidationError):
        make(migration_database_url="mysql://u:p@h/db")


def test_migrations_must_not_use_the_transaction_pooler():
    with pytest.raises(ValidationError, match="transaction pooler"):
        make(migration_database_url="postgresql://postgres.ref:pw@pooler.example.com:6543/postgres")
    make(migration_database_url="postgresql://postgres.ref:pw@pooler.example.com:5432/postgres")


def test_secrets_are_not_shown_when_the_settings_are_printed():
    password, key = "super" + "secretvalue", "sb_" + "secret_" + "hidden_value_123"   # built from pieces so this file holds nothing key-shaped
    s = make(database_url="postgresql://u:" + password + "@h:6543/db", supabase_secret_key=key)
    text = repr(s) + str(s)
    assert password not in text
    assert key not in text


def test_versions_are_read_from_a_comma_separated_list():
    s = make(supported_engine_versions="0.1.0, 0.2.0", supported_content_versions="abc-practice")
    assert s.supported_engine_versions == ["0.1.0", "0.2.0"]
    assert s.supported_content_versions == ["abc-practice"]


def test_limits_are_checked():
    for bad in ({"db_pool_size": 50}, {"db_pool_size": -1}, {"db_connect_timeout": 0}, {"db_connect_timeout": 600}, {"max_upload_bytes": 10}, {"max_upload_bytes": 100 * 1024 * 1024}):
        with pytest.raises(ValidationError):
            make(**bad)
