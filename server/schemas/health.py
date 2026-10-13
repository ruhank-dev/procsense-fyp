"""Response shapes of the health endpoints."""
from pydantic import BaseModel


class Health(BaseModel):
    status: str
    service: str
    app_env: str
    git_sha: str


class DbHealth(BaseModel):
    status: str
    reason: str = ""
    latency_ms: float = 0.0
    server_version: str = ""
    supported_engine_versions: list[str] = []
    supported_content_versions: list[str] = []
