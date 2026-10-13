"""Settings of the service: environment variables (and a git-ignored server/.env), validated once at start (INSTRUCTIONS §5: no credentials in code)."""
from __future__ import annotations

from functools import lru_cache
from pathlib import Path
from typing import Annotated, Literal
from urllib.parse import urlsplit

from pydantic import SecretStr, field_validator
from pydantic_settings import BaseSettings, NoDecode, SettingsConfigDict

TRANSACTION_POOLER_PORT = 6543


def _psycopg_url(value: str, what: str) -> str:
    """Accepts the address as Supabase prints it (postgresql://...) and makes it use the psycopg 3 driver. Anything that is not PostgreSQL is refused: the service is tested and run on PostgreSQL only."""
    if not value:
        return value
    if value.startswith("postgres://"):
        value = "postgresql://" + value[len("postgres://"):]
    if value.startswith("postgresql://"):
        value = "postgresql+psycopg://" + value[len("postgresql://"):]
    if not value.startswith("postgresql+psycopg://"):
        raise ValueError(f"{what} must be a PostgreSQL address (postgresql://...); this service does not run on any other database")
    return value


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=Path(__file__).resolve().parent / ".env", env_file_encoding="utf-8", extra="ignore")

    app_env: Literal["dev", "test", "prod"] = "dev"
    git_sha: str = "unknown"

    # Supabase project
    supabase_url: str = ""
    supabase_publishable_key: str = ""
    supabase_secret_key: SecretStr = SecretStr("")
    supabase_jwt_audience: str = "authenticated"   # the audience Supabase puts in a signed-in person's access token
    jwks_cache_seconds: int = 600                  # Supabase's own edge caches the key set for 10 minutes
    clock_leeway_seconds: int = 10                 # a few seconds of clock difference is tolerated when checking a token's expiry
    invitation_days: int = 14

    # the application uses the transaction pooler; migrations use the session pooler
    database_url: SecretStr = SecretStr("")
    migration_database_url: SecretStr = SecretStr("")
    db_pool_size: int = 3
    db_connect_timeout: int = 10   # seconds; a paused or unreachable free database must fail fast, not hang a request for minutes

    max_upload_bytes: int = 2 * 1024 * 1024
    supported_engine_versions: Annotated[list[str], NoDecode] = []
    supported_content_versions: Annotated[list[str], NoDecode] = []

    @field_validator("supported_engine_versions", "supported_content_versions", mode="before")
    @classmethod
    def _split(cls, value):
        if isinstance(value, str):
            return [part.strip() for part in value.split(",") if part.strip()]
        return value

    @field_validator("database_url", "migration_database_url", mode="after")
    @classmethod
    def _postgres_only(cls, value: SecretStr, info) -> SecretStr:
        url = _psycopg_url(value.get_secret_value(), info.field_name)
        if info.field_name == "migration_database_url" and url and urlsplit(url).port == TRANSACTION_POOLER_PORT:
            raise ValueError("migration_database_url must not use the transaction pooler (port 6543): migrations need a session connection (port 5432)")
        return SecretStr(url)

    @field_validator("db_pool_size")
    @classmethod
    def _pool(cls, value: int) -> int:
        if value < 0 or value > 10:
            raise ValueError("db_pool_size must be between 0 (no pool) and 10: the free database allows few connections")
        return value

    @field_validator("jwks_cache_seconds", "clock_leeway_seconds", "invitation_days")
    @classmethod
    def _positive_small(cls, value: int) -> int:
        if not 0 <= value <= 3600 * 24 * 60:
            raise ValueError("this limit is out of range")
        return value

    @field_validator("db_connect_timeout")
    @classmethod
    def _timeout(cls, value: int) -> int:
        if not 1 <= value <= 60:
            raise ValueError("db_connect_timeout must be between 1 and 60 seconds")
        return value

    @field_validator("max_upload_bytes")
    @classmethod
    def _upload(cls, value: int) -> int:
        if not 1024 <= value <= 20 * 1024 * 1024:
            raise ValueError("max_upload_bytes must be between 1 KB and 20 MB")
        return value


    @property
    def jwt_issuer(self) -> str:
        """The issuer Supabase Auth puts in its tokens: the project address plus /auth/v1."""
        return self.supabase_url.rstrip("/") + "/auth/v1"

    @property
    def jwks_url(self) -> str:
        return self.jwt_issuer + "/.well-known/jwks.json"


@lru_cache
def get_settings() -> Settings:
    return Settings()
