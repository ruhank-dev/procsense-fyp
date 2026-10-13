"""baseline: an empty first migration, so the database has a version table and every later change is a numbered step

Revision ID: 0001
Revises:
Create Date: 2026-10-13
"""
from alembic import op  # noqa: F401
import sqlalchemy as sa  # noqa: F401

revision = "0001"
down_revision = None
branch_labels = None
depends_on = None


def upgrade() -> None:
    pass


def downgrade() -> None:
    pass
