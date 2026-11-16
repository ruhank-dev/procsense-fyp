"""Response shapes of the LAN pairing routes (P3-NET-11, P3-NET-15)."""
import uuid

from pydantic import BaseModel


class DemoIdentityOut(BaseModel):
    """Who a paired headset plays as: a synthetic demonstration user in the synthetic demonstration organisation (never a real account)."""
    user_id: uuid.UUID
    pseudonym: str
    display_name: str
    organisation_id: uuid.UUID
    organisation_name: str
    role: str
