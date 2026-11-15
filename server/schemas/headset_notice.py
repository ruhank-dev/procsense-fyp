"""Server-push notices of the optional headset socket (P3-NET-12). The headset works the same without them."""
from typing import Literal

from pydantic import BaseModel, Field

NoticeKind = Literal["hello", "generation_progress", "shutdown", "quota"]


class Notice(BaseModel):
    kind: NoticeKind
    session_id: str = Field(max_length=64)
    progress: float | None = Field(default=None, ge=0.0, le=1.0)   # generation_progress only
    detail: str = Field(default="", max_length=200)                # a short code or sentence for the status chip, never a secret
