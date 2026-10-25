"""consent: the terms and the optional cloud AI processing answers, with the fingerprint of what was shown and where the answer came from

Revision ID: 0009
Revises: 0008
Create Date: 2026-10-25 12:00:00.000000+00:00
"""
from alembic import op
import sqlalchemy as sa


revision = '0009'
down_revision = '0008'
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.add_column('consent_record', sa.Column('text_sha256', sa.String(length=64), nullable=True))
    op.add_column('consent_record', sa.Column('via', sa.String(length=16), server_default=sa.text("'web'"), nullable=False))
    op.drop_constraint(op.f('ck_consent_record_kind_values'), 'consent_record', type_='check')
    op.create_check_constraint(op.f('ck_consent_record_kind_values'), 'consent_record', "kind in ('results', 'research', 'terms', 'cloud')")


def downgrade() -> None:
    op.execute("delete from consent_record where kind in ('terms', 'cloud')")
    op.drop_constraint(op.f('ck_consent_record_kind_values'), 'consent_record', type_='check')
    op.create_check_constraint(op.f('ck_consent_record_kind_values'), 'consent_record', "kind in ('results', 'research')")
    op.drop_column('consent_record', 'via')
    op.drop_column('consent_record', 'text_sha256')
