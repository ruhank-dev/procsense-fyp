"""Settings of the service: environment variables (and a git-ignored server/.env), validated once at start (INSTRUCTIONS §5: no credentials in code)."""
from __future__ import annotations

from functools import lru_cache
from pathlib import Path
from typing import Annotated, Literal
from urllib.parse import urlsplit

from pydantic import SecretStr, field_validator, model_validator
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
    max_package_bytes: int = 1024 * 1024
    live_generation: bool = False          # POST /scenario-packages/generate builds a package on request from the person's own history (DT-199); off by default: it needs the composer and content in the image
    package_pool_dir: str = ""             # a folder of pre-generated packages (tools/generation/pool.py makes it: index.json and *.package.json); empty = POST /scenario-packages/next answers 503 no_pool
    # verification (p1-backend-s09): the engine's command line and the content packages, one folder per content version (practice and assessment each have their own version)
    engine_cli: str = ""                   # the command that runs `procsense-sim`, e.g. /opt/engine/procsense-sim, or "dotnet /path/ProcSense.Cli.dll"; empty = verification is off and sessions wait as pending
    engine_content_root: str = ""          # a folder holding one subfolder per content version the service can verify
    verify_timeout_seconds: int = 60
    verify_batch: int = 5
    verify_on_upload: bool = True
    internal_key: SecretStr = SecretStr("")   # lets a scheduled job call /internal/verify-pending; empty = that route is closed
    upload_rate_per_hour: int = 120
    # AI gateway (p1-backend-s06): the model host is Ollama on the machine the service runs next to; empty ollama_url switches every model call off (the routes answer with the fallback signal)
    ollama_url: str = "http://127.0.0.1:11434"   # 127.0.0.1, not localhost: on Windows the name costs a flat 2 s per request
    ai_rate_per_hour: int = 600                  # model calls per person per hour, in memory
    ai_queue_wait_seconds: float = 5.0           # how long a request waits for the one model slot before the fallback signal (a 6 GB GPU runs one model call at a time)
    asr_model: str = "small.en"                  # speech recognition (p1-voice-input-s01): faster-whisper, an optional dependency (uv sync --extra asr)
    asr_device: str = "auto"                     # "cuda" on the desktop with a card (needs the asr-gpu extra on Windows), "cpu" otherwise
    asr_compute: str = "int8"                    # "int8_float16" on the card: 0.57 s for a 3 to 7 s clip beside the resident 4B model, against 6 s on the processor (reports/ai/asr-wer.json)
    ai_features_override: str = ""               # features forced on for development and tests ("all" or names); empty in production: the evaluation gate decides (content/ai/features.json)
    ai_live_timeout_seconds: float = 20.0
    # speech evaluation (p1-ai-language-s22, ADR-0025): gemma4:31b on Ollama Cloud judges what the trainee's speech does (structured output); the engine scores. The route is closed by the `speech_evaluation` evaluation gate until an independent evaluation passes
    speech_eval_model: str = "gemma4:31b-cloud"              # through a signed-in local Ollama use the `-cloud` form; for the direct API use "gemma4:31b" with the base url and key below (https://ollama.com/api/tags lists the names)
    speech_eval_base_url: str = ""                           # empty = the local Ollama (ollama_url), which relays a cloud model when signed in; "https://ollama.com" = the direct API with the key below
    speech_eval_api_key: SecretStr = SecretStr("")           # direct API only; never logged, never in a request body, never committed (.env or the host's secret store)
    speech_eval_timeout_seconds: float = 15.0                # one attempt (the measured median is 2.4 s, p95 about 4 s, free plan)
    speech_eval_budget_ms: int = 25000                       # total wall budget for one evaluation including a retry
    speech_eval_max_attempts: int = 2
    # per-task model routing (ADR-0026, content/ai/model_routes.json): both OFF, every task then uses the local model
    cloud_routing_enabled: bool = False            # a task whose route names an Ollama Cloud model may use it (free plan; any outage, usage limit or invalid reply falls back to the local model)
    cloud_trainee_data_approved: bool = False      # routes that carry what a trainee said or did may go to the cloud; waits for the privacy decision (DT-248, DT-127)
    cloud_synthetic_deployment: bool = False      # the owner declares this deployment handles synthetic data only (development, demonstrations): it releases the trainee-class routes; never set it where real trainees play
    ai_batch_timeout_seconds: float = 600.0
    # privacy (p1-backend-s12): design values to be confirmed with a qualified person (DT-127)
    consent_required_for_upload: bool = True   # a session is accepted only from a person who agreed to the processing of their results (the current text)
    retention_session_months: int = 24         # after this the stored log and report page of a session are removed (the results stay); an organisation may choose 6 to 60
    retention_access_log_months: int = 24
    retention_ai_call_months: int = 12         # the AI audit rows (p1-backend-s05): many small rows, so a shorter period than the sessions
    audit_queue_size: int = 10_000             # AI audit rows waiting to be written; when full a row is dropped and the drop is logged (the reply is never held up)
    # headset sign-in with a code (p1-backend-s10)
    device_verification_uri: str = ""      # where a person enters the code (the web dashboard's /device page); empty = this service's own /device address
    device_login_minutes: int = 10
    device_poll_seconds: int = 5
    device_access_minutes: int = 15
    device_refresh_days: int = 30          # a headset not used for this long must sign in again
    device_start_per_hour: int = 30        # per caller address, kept in memory
    device_decide_per_hour: int = 30       # tries to approve a code, per person: guessing a code is not possible in practice, and this makes it slower still
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

    @field_validator("max_upload_bytes", "max_package_bytes")
    @classmethod
    def _upload(cls, value: int) -> int:
        if not 1024 <= value <= 20 * 1024 * 1024:
            raise ValueError("an upload limit must be between 1 KB and 20 MB")
        return value

    @field_validator("verify_timeout_seconds")
    @classmethod
    def _verify_timeout(cls, value: int) -> int:
        if not 5 <= value <= 600:
            raise ValueError("verify_timeout_seconds must be between 5 and 600")
        return value

    @field_validator("verify_batch")
    @classmethod
    def _verify_batch(cls, value: int) -> int:
        if not 1 <= value <= 50:
            raise ValueError("verify_batch must be between 1 and 50")
        return value

    @field_validator("device_login_minutes", "device_poll_seconds", "device_access_minutes", "device_refresh_days", "device_start_per_hour", "device_decide_per_hour")
    @classmethod
    def _device_limits(cls, value: int) -> int:
        if not 1 <= value <= 100000:
            raise ValueError("a device sign-in setting must be between 1 and 100000")
        return value

    @field_validator("retention_session_months", "retention_access_log_months", "retention_ai_call_months")
    @classmethod
    def _retention(cls, value: int) -> int:
        if not 6 <= value <= 60:
            raise ValueError("a retention period must be between 6 and 60 months")
        return value

    @field_validator("upload_rate_per_hour")
    @classmethod
    def _rate(cls, value: int) -> int:
        if not 1 <= value <= 100000:
            raise ValueError("upload_rate_per_hour must be between 1 and 100000")
        return value


    def engine_command(self) -> list[str]:
        """The engine's command line as a list: a JSON array (["dotnet", "/path/ProcSense.Cli.dll"]) or a plain path to the executable. Empty when verification is not configured."""
        text = self.engine_cli.strip()
        if not text or not self.engine_content_root.strip():
            return []
        if text.startswith("["):
            import json
            parts = json.loads(text)
            if not (isinstance(parts, list) and parts and all(isinstance(p, str) and p for p in parts)):
                raise ValueError("engine_cli must be a path or a JSON list of strings")
            return parts
        return [text]

    @property
    def jwt_issuer(self) -> str:
        """The issuer Supabase Auth puts in its tokens: the project address plus /auth/v1."""
        return self.supabase_url.rstrip("/") + "/auth/v1"

    @property
    def jwks_url(self) -> str:
        return self.jwt_issuer + "/.well-known/jwks.json"

    @model_validator(mode="after")
    def _synthetic_deployment_is_never_a_real_environment(self):
        """CLOUD_SYNTHETIC_DEPLOYMENT is the owner's declaration that the deployment handles invented data only (the FYP demonstration). It releases trainee-class routes to the cloud without the personal consent, so it must never sit next to a production environment or next to the
        record that real trainees' data is approved: a service started with either combination refuses to start."""
        if self.cloud_synthetic_deployment and self.app_env == "prod":
            raise ValueError("CLOUD_SYNTHETIC_DEPLOYMENT is for the synthetic demonstration only and cannot be combined with APP_ENV=prod")
        if self.cloud_synthetic_deployment and self.cloud_trainee_data_approved:
            raise ValueError("CLOUD_SYNTHETIC_DEPLOYMENT (invented data only) cannot be combined with CLOUD_TRAINEE_DATA_APPROVED (real trainees): choose one")
        return self


@lru_cache
def get_settings() -> Settings:
    return Settings()
