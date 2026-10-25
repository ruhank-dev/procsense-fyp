"""Approved content packages for the headset (DT-131, p1-backend-s09): the service already holds one folder of content per version it can verify (`ENGINE_CONTENT_ROOT`, made by tools/content/package_versions.py);
the headset can now ask which versions there are and download one, so a headset and the service always play and replay the same content.

    GET /content/versions                       the versions held, with their mode (practice or assessment), file count, size and content hash
    GET /content/{version}/manifest             the version's manifest.json (every file and its hash)
    GET /content/{version}/archive              a zip of the version, in a fixed order with fixed dates so the same version is the same bytes; the header X-Content-SHA256 holds its hash

Only the versions named like `<12 hex>-practice` or `<12 hex>-assessment` are served: the development folder (named by the 12 characters alone) is never offered, and a name that is not one of those never reaches the file system.
Any active member (or platform administrator) may fetch them: a headset signs in as a member. Nothing here is personal data.
"""
from __future__ import annotations

import hashlib
import io
import json
import re
import zipfile
from pathlib import Path

from fastapi import APIRouter, Depends, HTTPException, Response

from server.auth.dependencies import CurrentUser, current_user
from server.config import Settings, get_settings

router = APIRouter(tags=["content"])
VERSION = re.compile(r"^[0-9a-f]{12}-(practice|assessment)$")
MAX_ARCHIVE_BYTES = 64 * 1024 * 1024


def _active_member(user: CurrentUser) -> None:
    if not any(m.status == "active" for m in user.memberships) and not user.profile.is_platform_admin:
        raise HTTPException(status_code=403, detail="no_active_membership")


def _root(settings: Settings) -> Path:
    if not settings.engine_content_root.strip():
        raise HTTPException(status_code=503, detail={"code": "no_content"})
    return Path(settings.engine_content_root)


def _folder(settings: Settings, version: str) -> Path:
    if not VERSION.match(version):
        raise HTTPException(status_code=404, detail="unknown_version")
    folder = _root(settings) / version
    if not (folder / "manifest.json").is_file():
        raise HTTPException(status_code=404, detail="unknown_version")
    return folder


def _files(folder: Path) -> list[Path]:
    return sorted((p for p in folder.rglob("*") if p.is_file()), key=lambda p: p.relative_to(folder).as_posix())


@router.get("/content/versions")
def versions(user: CurrentUser = Depends(current_user), settings: Settings = Depends(get_settings)) -> dict:
    _active_member(user)
    root = _root(settings)
    out = []
    for folder in sorted(p for p in root.iterdir() if p.is_dir() and VERSION.match(p.name)) if root.is_dir() else []:
        try:
            manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        files = _files(folder)
        out.append({"version": folder.name, "mode": folder.name.rsplit("-", 1)[1], "content_hash": manifest.get("content_hash", ""), "files": len(files), "bytes": sum(p.stat().st_size for p in files)})
    return {"versions": out}


@router.get("/content/{version}/manifest")
def manifest(version: str, user: CurrentUser = Depends(current_user), settings: Settings = Depends(get_settings)) -> Response:
    _active_member(user)
    return Response(content=(_folder(settings, version) / "manifest.json").read_bytes(), media_type="application/json")


@router.get("/content/{version}/archive")
def archive(version: str, user: CurrentUser = Depends(current_user), settings: Settings = Depends(get_settings)) -> Response:
    _active_member(user)
    folder = _folder(settings, version)
    files = _files(folder)
    if sum(p.stat().st_size for p in files) > MAX_ARCHIVE_BYTES:
        raise HTTPException(status_code=413, detail="archive_too_large")
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for p in files:
            info = zipfile.ZipInfo(p.relative_to(folder).as_posix(), date_time=(2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(info, p.read_bytes())
    data = buffer.getvalue()
    return Response(content=data, media_type="application/zip", headers={"X-Content-SHA256": hashlib.sha256(data).hexdigest(), "Cache-Control": "no-store",
                                                                           "Content-Disposition": f'attachment; filename="procsense-content-{version}.zip"'})
