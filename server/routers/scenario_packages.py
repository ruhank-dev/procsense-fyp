"""Generated scenario packages, stored once by the SHA-256 of their bytes (p1-backend-s02).

A session of a generated scenario names its package in the header; the server needs the package to replay the session. The headset sends it here when the upload answers `package_needed`.
The hash in the address must be the hash of the bytes sent, so nobody can store one thing under another's name, and a package is stored once however many sessions use it.
"""
from __future__ import annotations

import hashlib
import json
import re

from fastapi import APIRouter, Depends, HTTPException, Request, Response
from sqlalchemy.orm import Session as DbSession

from server.auth.dependencies import CurrentUser, current_user, db_session
from server.config import Settings, get_settings
from server.repositories import sessions as repository
from server.schemas.sessions import PackageReply

router = APIRouter(tags=["scenario packages"])
HASH = re.compile(r"^[0-9a-f]{64}$")


@router.put("/scenario-packages/{package_hash}", response_model=PackageReply, status_code=201)
async def put_package(package_hash: str, request: Request, response: Response, user: CurrentUser = Depends(current_user), db: DbSession = Depends(db_session),
                      settings: Settings = Depends(get_settings)) -> PackageReply:
    if not HASH.match(package_hash):
        raise HTTPException(status_code=422, detail={"code": "BAD_HASH"})
    if not any(m.status == "active" for m in user.memberships) and not user.profile.is_platform_admin:
        raise HTTPException(status_code=403, detail="no_active_membership")
    raw = b""
    async for chunk in request.stream():
        raw += chunk
        if len(raw) > settings.max_package_bytes:
            raise HTTPException(status_code=413, detail="too_large")
    if hashlib.sha256(raw).hexdigest() != package_hash:
        raise HTTPException(status_code=422, detail={"code": "HASH_MISMATCH"})
    try:
        if not isinstance(json.loads(raw.decode("utf-8")), dict):
            raise ValueError
    except (UnicodeDecodeError, ValueError):
        raise HTTPException(status_code=422, detail={"code": "NOT_A_JSON_OBJECT"}) from None
    if not repository.store_package(db, package_hash, raw):
        response.status_code = 200
        return PackageReply(package_hash=package_hash, outcome="already_stored")
    db.commit()
    return PackageReply(package_hash=package_hash, outcome="stored")
