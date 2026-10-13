"""Consent, the access log, export and deletion of one's own data, and an organisation's retention period (p1-backend-s12)."""
from __future__ import annotations

import logging

from fastapi import APIRouter, Depends, HTTPException, Response
from fastapi.encoders import jsonable_encoder
from sqlalchemy.orm import Session as DbSession

from server import privacy
from server.auth.dependencies import CurrentUser, OrgContext, current_user, db_session, get_admin, require_org_admin, web_principal
from server.auth.supabase_admin import DeleteUserError, SupabaseAdmin
from server.auth.tokens import Principal
from server.config import Settings, get_settings
from server.repositories import privacy as repository
from server.schemas.privacy import AccessEntry, ConsentIn, ConsentKind, ConsentState, DeleteIn, RetentionIn, RetentionOut

router = APIRouter(tags=["privacy"])
log = logging.getLogger("procsense.privacy")


def _state(db: DbSession, user: CurrentUser) -> ConsentState:
    latest = repository.latest_consents(db, user.profile.user_id)
    kinds = []
    for kind, text in privacy.CONSENT_TEXTS.items():
        row = latest.get(kind)
        current = row is not None and row.text_version == privacy.CONSENT_VERSION and (row.granted or not text["required"])
        kinds.append(ConsentKind(kind=kind, title=text["title"], text=text["text"], required=text["required"], granted=row.granted if row else None,
                                 answered_version=row.text_version if row else None, current=current))
    return ConsentState(version=privacy.CONSENT_VERSION, needs_consent=any(k.required and not (k.current and k.granted) for k in kinds), kinds=kinds)


@router.get("/privacy/consent", response_model=ConsentState)
def read_consent(user: CurrentUser = Depends(current_user), db: DbSession = Depends(db_session)) -> ConsentState:
    """What the person is asked, what they answered, and whether they must answer (again) before their sessions can be stored."""
    return _state(db, user)


@router.post("/privacy/consent", response_model=ConsentState)
def give_consent(body: ConsentIn, user: CurrentUser = Depends(current_user), _web: Principal = Depends(web_principal), db: DbSession = Depends(db_session)) -> ConsentState:
    """Records an answer (every answer is kept, the latest counts). Only on the web: a headset cannot agree on someone's behalf."""
    repository.record_consent(db, user.profile.user_id, body.kind, body.granted)
    db.commit()
    return _state(db, user)


@router.get("/privacy/access-log", response_model=list[AccessEntry])
def who_looked(user: CurrentUser = Depends(current_user), _web: Principal = Depends(web_principal), db: DbSession = Depends(db_session)) -> list[AccessEntry]:
    """Who opened this person's reports or progress, or exported data that includes them."""
    return [AccessEntry(**row) for row in repository.access_log_about(db, user.profile.user_id)]


@router.get("/privacy/export")
def export_my_data(user: CurrentUser = Depends(current_user), _web: Principal = Depends(web_principal), db: DbSession = Depends(db_session)) -> Response:
    """Everything held about the person, as one JSON file."""
    import json
    body = json.dumps(jsonable_encoder(repository.export_person(db, user.profile)), indent=1, ensure_ascii=False)
    return Response(content=body, media_type="application/json", headers={"Content-Disposition": 'attachment; filename="my-procsense-data.json"', "Cache-Control": "no-store"})


@router.post("/privacy/delete-my-data", status_code=204)
def delete_my_data(body: DeleteIn, user: CurrentUser = Depends(current_user), _web: Principal = Depends(web_principal), db: DbSession = Depends(db_session),
                   admin: SupabaseAdmin = Depends(get_admin)) -> Response:
    """Deletes the person's sign-in account and cuts every link to their sessions (the sessions stay, so totals do not change). Cannot be undone."""
    if body.confirm.strip().upper() != "DELETE":
        raise HTTPException(status_code=422, detail="type_DELETE_to_confirm")
    user_id = user.profile.user_id
    try:
        repository.delete_person(db, user_id)
    except repository.LastAdministrator:
        db.rollback()
        raise HTTPException(status_code=409, detail="last_administrator") from None
    try:
        admin.delete_user(str(user_id))   # before the commit: if the account cannot be removed, nothing is cut and the person can try again
    except DeleteUserError as e:
        db.rollback()
        log.error("deleting an account failed: %s", e)
        raise HTTPException(status_code=502, detail="account_service_unavailable") from None
    db.commit()
    log.info("a person deleted their data")
    return Response(status_code=204)


@router.get("/organisations/{organisation_id}/retention", response_model=RetentionOut)
def read_retention(ctx: OrgContext = Depends(require_org_admin), settings: Settings = Depends(get_settings)) -> RetentionOut:
    months = ctx.organisation.retention_months
    return RetentionOut(retention_months=months, effective_months=months or settings.retention_session_months, default_months=settings.retention_session_months)


@router.put("/organisations/{organisation_id}/retention", response_model=RetentionOut)
def set_retention(body: RetentionIn, ctx: OrgContext = Depends(require_org_admin), db: DbSession = Depends(db_session), settings: Settings = Depends(get_settings)) -> RetentionOut:
    """How long this organisation's stored session logs are kept (6 to 60 months; empty puts the platform default back)."""
    ctx.organisation.retention_months = body.retention_months
    db.commit()
    return RetentionOut(retention_months=body.retention_months, effective_months=body.retention_months or settings.retention_session_months, default_months=settings.retention_session_months)
