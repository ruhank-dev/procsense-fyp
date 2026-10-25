"""generation history (DT-201): the generation_history table, with the same second lock as the other tables

Revision ID: 0010
Revises: 0009
Create Date: 2026-10-26 08:00:00+00:00
"""
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql

revision = '0010'
down_revision = '0009'
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table('generation_history',
    sa.Column('session_id', sa.UUID(), nullable=False),
    sa.Column('organisation_id', sa.UUID(), nullable=False),
    sa.Column('user_id', sa.UUID(), nullable=False),
    sa.Column('case_id', sa.String(length=64), nullable=False),
    sa.Column('story', sa.String(length=64), nullable=False),
    sa.Column('tier', sa.String(length=16), nullable=False),
    sa.Column('behaviours', postgresql.JSONB(astext_type=sa.Text()), nullable=False),
    sa.Column('ended_at', sa.DateTime(timezone=True), nullable=False),
    sa.Column('package_id', sa.String(length=64), nullable=False),
    sa.Column('package_sha256', sa.String(length=64), nullable=False),
    sa.Column('fingerprint', sa.String(length=128), nullable=False),
    sa.Column('levels', postgresql.JSONB(astext_type=sa.Text()), nullable=False),
    sa.Column('recorded_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
    sa.ForeignKeyConstraint(['organisation_id'], ['organisation.id'], name=op.f('fk_generation_history_organisation_id_organisation')),
    sa.ForeignKeyConstraint(['user_id'], ['profile.user_id'], name=op.f('fk_generation_history_user_id_profile')),
    sa.PrimaryKeyConstraint('session_id', name=op.f('pk_generation_history'))
    )
    op.create_index('ix_generation_history_user_ended', 'generation_history', ['user_id', 'ended_at'], unique=False)
    op.execute("ALTER TABLE generation_history ENABLE ROW LEVEL SECURITY")
    op.execute("""
        DO $$
        BEGIN
            IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'anon') THEN
                EXECUTE 'REVOKE ALL ON TABLE generation_history FROM anon';
            END IF;
            IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'authenticated') THEN
                EXECUTE 'REVOKE ALL ON TABLE generation_history FROM authenticated';
            END IF;
        END
        $$
    """)


def downgrade() -> None:
    op.drop_index('ix_generation_history_user_ended', table_name='generation_history')
    op.drop_table('generation_history')
