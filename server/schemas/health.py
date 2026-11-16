"""Response shapes of the health endpoints."""
from pydantic import BaseModel


class Health(BaseModel):
    status: str
    service: str
    app_env: str
    git_sha: str
    api_version: int = 1                       # P3-NET-10: what a headset checks before it trusts the AI routes
    engine_versions: list[str] = []
    content_versions: list[str] = []
    lan_profile: bool = False


class DbHealth(BaseModel):
    status: str
    reason: str = ""
    latency_ms: float = 0.0
    server_version: str = ""
    supported_engine_versions: list[str] = []
    supported_content_versions: list[str] = []
