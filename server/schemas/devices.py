"""Request and reply shapes for the device-code login (p1-backend-s10)."""
from __future__ import annotations

import datetime as dt
import uuid

from pydantic import BaseModel, Field, field_validator


class DeviceStartIn(BaseModel):
    device_name: str = Field(default="Headset", max_length=60)

    @field_validator("device_name")
    @classmethod
    def _name(cls, value: str) -> str:
        value = " ".join(value.split())
        return value or "Headset"


class DeviceStartOut(BaseModel):
    device_code: str          # secret: the headset keeps it and sends it when polling
    user_code: str            # what the person types or confirms, like BCDF-GHJK
    verification_uri: str
    verification_uri_complete: str
    expires_in: int
    interval: int


class DevicePollIn(BaseModel):
    device_code: str = Field(min_length=20, max_length=200)


class DeviceRefreshIn(BaseModel):
    refresh_token: str = Field(min_length=20, max_length=200)


class DeviceTokensOut(BaseModel):
    access_token: str
    token_type: str = "Bearer"
    expires_in: int           # seconds from now: the headset counts it with its own timer and never from its date
    refresh_token: str
    refresh_expires_in: int


class DeviceDecisionIn(BaseModel):
    user_code: str = Field(min_length=4, max_length=20)
    approve: bool = True


class DeviceDecisionOut(BaseModel):
    device_name: str
    status: str


class DeviceOut(BaseModel):
    id: uuid.UUID
    device_name: str
    created_at: dt.datetime
    last_used_at: dt.datetime
