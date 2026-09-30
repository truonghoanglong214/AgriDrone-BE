-- Fresh BE1 baseline. This migration is intentionally designed from the target
-- ownership model; it is not a replay of the former consolidated EF history.

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE EXTENSION IF NOT EXISTS postgis;

CREATE SCHEMA identity;
CREATE SCHEMA farm;
CREATE SCHEMA plant;
CREATE SCHEMA survey;
CREATE SCHEMA notification;
CREATE SCHEMA audit;
CREATE SCHEMA messaging;

REVOKE ALL ON SCHEMA identity, farm, plant, survey, notification, audit, messaging FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA identity, farm, plant, survey, notification, audit, messaging TO CURRENT_USER;

-- Identity: tenant membership is deliberately owner-only. Legacy tenant staff,
-- farm membership and zone assignment tables do not exist in this baseline.
CREATE TABLE identity.users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email citext NOT NULL,
    password_hash text NOT NULL,
    full_name varchar(150) NOT NULL,
    phone varchar(30),
    status varchar(20) NOT NULL DEFAULT 'ACTIVE'
        CONSTRAINT ck_users_status CHECK (status IN ('ACTIVE', 'INACTIVE', 'LOCKED')),
    last_login_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz,
    CONSTRAINT uq_users_email UNIQUE (email)
);

CREATE TABLE identity.roles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(50) NOT NULL CONSTRAINT uq_roles_code UNIQUE,
    name varchar(100) NOT NULL,
    description text,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE identity.user_roles (
    user_id uuid NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    role_id uuid NOT NULL REFERENCES identity.roles(id) ON DELETE CASCADE,
    assigned_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_user_roles PRIMARY KEY (user_id, role_id)
);

CREATE TABLE identity.tenants (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(50) NOT NULL,
    name varchar(150) NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'ACTIVE'
        CONSTRAINT ck_tenants_status CHECK (status IN ('ACTIVE', 'INACTIVE')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz
);
CREATE UNIQUE INDEX ux_tenants_code_active ON identity.tenants(code) WHERE deleted_at IS NULL;

CREATE TABLE identity.tenant_memberships (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES identity.tenants(id) ON DELETE CASCADE,
    user_id uuid NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    role varchar(20) NOT NULL DEFAULT 'OWNER'
        CONSTRAINT ck_tenant_memberships_owner_only CHECK (role = 'OWNER'),
    status varchar(20) NOT NULL DEFAULT 'ACTIVE'
        CONSTRAINT ck_tenant_memberships_status CHECK (status IN ('ACTIVE', 'INACTIVE')),
    joined_at timestamptz NOT NULL DEFAULT now(),
    created_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_tenant_memberships_version CHECK (version > 0),
    CONSTRAINT uq_tenant_memberships_tenant_user UNIQUE (tenant_id, user_id)
);
CREATE UNIQUE INDEX uq_tenant_memberships_active_owner
    ON identity.tenant_memberships(tenant_id) WHERE role = 'OWNER' AND status = 'ACTIVE';
CREATE INDEX ix_tenant_memberships_user ON identity.tenant_memberships(user_id);

CREATE TABLE identity.tenant_invitations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES identity.tenants(id) ON DELETE CASCADE,
    email citext NOT NULL,
    role varchar(20) NOT NULL DEFAULT 'OWNER'
        CONSTRAINT ck_tenant_invitations_owner_only CHECK (role = 'OWNER'),
    purpose varchar(30) NOT NULL DEFAULT 'OWNER_PROVISIONING'
        CONSTRAINT ck_tenant_invitations_purpose CHECK (purpose = 'OWNER_PROVISIONING'),
    token_hash char(64) NOT NULL CONSTRAINT uq_tenant_invitations_token_hash UNIQUE,
    status varchar(20) NOT NULL DEFAULT 'PENDING'
        CONSTRAINT ck_tenant_invitations_status CHECK (status IN ('PENDING', 'ACCEPTED', 'REVOKED', 'EXPIRED')),
    invited_by_user_id uuid NOT NULL REFERENCES identity.users(id) ON DELETE RESTRICT,
    accepted_by_user_id uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    accepted_at timestamptz,
    CONSTRAINT ck_tenant_invitations_expiration CHECK (expires_at > created_at),
    CONSTRAINT ck_tenant_invitations_acceptance CHECK (
        (status = 'ACCEPTED' AND accepted_by_user_id IS NOT NULL AND accepted_at IS NOT NULL)
        OR (status <> 'ACCEPTED' AND accepted_at IS NULL))
);
CREATE UNIQUE INDEX uq_tenant_invitations_pending_tenant_email
    ON identity.tenant_invitations(tenant_id, email) WHERE status = 'PENDING';
CREATE UNIQUE INDEX uq_tenant_invitations_pending_owner_provisioning
    ON identity.tenant_invitations(tenant_id) WHERE status = 'PENDING';
CREATE INDEX ix_tenant_invitations_status_expiration ON identity.tenant_invitations(status, expires_at);

CREATE TABLE identity.password_reset_tokens (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    token_hash char(64) NOT NULL CONSTRAINT uq_password_reset_tokens_token_hash UNIQUE,
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    used_at timestamptz,
    revoked_at timestamptz,
    CONSTRAINT ck_password_reset_tokens_expiration CHECK (expires_at > created_at),
    CONSTRAINT ck_password_reset_tokens_terminal CHECK (used_at IS NULL OR revoked_at IS NULL)
);
CREATE UNIQUE INDEX uq_password_reset_tokens_active_user
    ON identity.password_reset_tokens(user_id) WHERE used_at IS NULL AND revoked_at IS NULL;
CREATE INDEX ix_password_reset_tokens_expiration ON identity.password_reset_tokens(expires_at);

CREATE TABLE identity.system_manager_profiles (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES identity.users(id) ON DELETE RESTRICT,
    status varchar(20) NOT NULL CONSTRAINT ck_system_manager_profiles_status CHECK (status IN ('ACTIVE', 'SUSPENDED')),
    availability varchar(20) NOT NULL CONSTRAINT ck_system_manager_profiles_availability CHECK (availability IN ('AVAILABLE', 'UNAVAILABLE')),
    qualification_status varchar(20) NOT NULL
        CONSTRAINT ck_system_manager_profiles_qualification CHECK (qualification_status IN ('PENDING', 'QUALIFIED', 'SUSPENDED', 'REVOKED')),
    qualification_expires_at timestamptz,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_system_manager_profiles_version CHECK (version > 0),
    CONSTRAINT uq_system_manager_profiles_user UNIQUE (user_id)
);
CREATE INDEX ix_system_manager_profiles_assignability ON identity.system_manager_profiles
    (status, availability, qualification_status, qualification_expires_at);

CREATE TABLE identity.system_manager_invitations (
    id uuid PRIMARY KEY,
    email citext NOT NULL,
    token_hash char(64) NOT NULL CONSTRAINT uq_system_manager_invitations_token_hash UNIQUE,
    status varchar(20) NOT NULL CONSTRAINT ck_system_manager_invitations_status CHECK (status IN ('PENDING', 'ACCEPTED', 'REVOKED', 'EXPIRED')),
    invited_by_user_id uuid NOT NULL REFERENCES identity.users(id) ON DELETE RESTRICT,
    accepted_by_user_id uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL,
    accepted_at timestamptz,
    CONSTRAINT ck_system_manager_invitations_expiration CHECK (expires_at > created_at),
    CONSTRAINT ck_system_manager_invitations_acceptance CHECK (
        (status = 'ACCEPTED' AND accepted_by_user_id IS NOT NULL AND accepted_at IS NOT NULL)
        OR (status <> 'ACCEPTED' AND accepted_at IS NULL))
);
CREATE UNIQUE INDEX uq_system_manager_invitations_pending_email
    ON identity.system_manager_invitations(email) WHERE status = 'PENDING';
CREATE INDEX ix_system_manager_invitations_status_expiration
    ON identity.system_manager_invitations(status, expires_at);

CREATE TABLE identity.initialization_locks (
    name varchar(100) PRIMARY KEY,
    version bigint NOT NULL DEFAULT 0 CONSTRAINT ck_initialization_locks_version CHECK (version >= 0)
);

-- Farm and PostGIS invariants.
CREATE TABLE farm.farms (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES identity.tenants(id) ON DELETE RESTRICT,
    code varchar(30) NOT NULL,
    name varchar(150) NOT NULL,
    address text,
    boundary geometry(Polygon,4326),
    center_point geometry(Point,4326),
    area_hectares numeric(12,4),
    status varchar(20) NOT NULL DEFAULT 'ACTIVE' CONSTRAINT ck_farms_status CHECK (status IN ('ACTIVE', 'INACTIVE')),
    created_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz,
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_farms_version CHECK (version > 0),
    CONSTRAINT uq_farms_id_tenant UNIQUE (id, tenant_id),
    CONSTRAINT ck_farms_area_nonnegative CHECK (area_hectares IS NULL OR area_hectares >= 0),
    CONSTRAINT ck_farms_boundary_valid CHECK (boundary IS NULL OR ST_IsValid(boundary))
);
CREATE UNIQUE INDEX ux_farms_tenant_code_active ON farm.farms(tenant_id, code) WHERE deleted_at IS NULL;
CREATE INDEX ix_farms_tenant ON farm.farms(tenant_id);
CREATE INDEX ix_farms_boundary_gist ON farm.farms USING gist(boundary);
CREATE INDEX ix_farms_center_point_gist ON farm.farms USING gist(center_point);

CREATE TABLE farm.farm_zones (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    farm_id uuid NOT NULL REFERENCES farm.farms(id) ON DELETE CASCADE,
    code varchar(30) NOT NULL,
    name varchar(100) NOT NULL,
    boundary geometry(Polygon,4326),
    area_hectares numeric(12,4),
    status varchar(20) NOT NULL DEFAULT 'ACTIVE' CONSTRAINT ck_farm_zones_status CHECK (status IN ('ACTIVE', 'INACTIVE')),
    created_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz,
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_farm_zones_version CHECK (version > 0),
    CONSTRAINT uq_farm_zones_id_farm UNIQUE (id, farm_id),
    CONSTRAINT ck_farm_zones_area_nonnegative CHECK (area_hectares IS NULL OR area_hectares >= 0),
    CONSTRAINT ck_farm_zones_boundary_valid CHECK (boundary IS NULL OR ST_IsValid(boundary))
);
CREATE UNIQUE INDEX ux_farm_zones_farm_code_active ON farm.farm_zones(farm_id, code) WHERE deleted_at IS NULL;
CREATE INDEX ix_farm_zones_farm ON farm.farm_zones(farm_id);
CREATE INDEX ix_farm_zones_boundary_gist ON farm.farm_zones USING gist(boundary);

CREATE OR REPLACE FUNCTION farm.enforce_zone_geometry() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE farm_boundary geometry;
BEGIN
    IF NEW.boundary IS NULL THEN RETURN NEW; END IF;
    SELECT boundary INTO farm_boundary FROM farm.farms WHERE id = NEW.farm_id;
    IF farm_boundary IS NULL OR NOT ST_Covers(farm_boundary, NEW.boundary) THEN
        RAISE EXCEPTION 'farm zone boundary must be covered by its farm boundary' USING ERRCODE = '23514';
    END IF;
    IF EXISTS (
        SELECT 1 FROM farm.farm_zones z
        WHERE z.farm_id = NEW.farm_id AND z.id <> NEW.id AND z.deleted_at IS NULL
          AND z.boundary IS NOT NULL
          -- Reject every positive-area interior intersection, including the
          -- containment case that ST_Overlaps alone intentionally excludes.
          AND ST_Relate(z.boundary, NEW.boundary, '2********')
    ) THEN
        RAISE EXCEPTION 'active farm zone boundaries must not overlap' USING ERRCODE = '23P01';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_farm_zones_geometry
    BEFORE INSERT OR UPDATE OF farm_id, boundary, deleted_at ON farm.farm_zones
    FOR EACH ROW EXECUTE FUNCTION farm.enforce_zone_geometry();

CREATE TABLE identity.farm_manager_assignments (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    system_manager_profile_id uuid NOT NULL REFERENCES identity.system_manager_profiles(id) ON DELETE RESTRICT,
    assigned_by uuid NOT NULL REFERENCES identity.users(id) ON DELETE RESTRICT,
    assignment_reason varchar(1000) NOT NULL,
    assigned_at timestamptz NOT NULL,
    ended_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    end_reason varchar(1000),
    ended_at timestamptz,
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_farm_manager_assignments_version CHECK (version > 0),
    CONSTRAINT fk_farm_manager_assignments_farm_tenant FOREIGN KEY (farm_id, tenant_id)
        REFERENCES farm.farms(id, tenant_id) ON DELETE RESTRICT,
    CONSTRAINT ck_farm_manager_assignments_end CHECK (
        (ended_at IS NULL AND ended_by IS NULL AND end_reason IS NULL)
        OR (ended_at IS NOT NULL AND ended_by IS NOT NULL AND length(btrim(end_reason)) > 0 AND ended_at >= assigned_at))
);
CREATE UNIQUE INDEX uq_farm_manager_assignments_active_farm
    ON identity.farm_manager_assignments(farm_id) WHERE ended_at IS NULL;
CREATE INDEX ix_farm_manager_assignments_profile_active
    ON identity.farm_manager_assignments(system_manager_profile_id, ended_at);
CREATE INDEX ix_farm_manager_assignments_history
    ON identity.farm_manager_assignments(tenant_id, farm_id, assigned_at DESC);

-- Survey catalogue and request/order workflow.
CREATE TABLE survey.survey_services (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(50) NOT NULL CONSTRAINT uq_survey_services_code UNIQUE,
    name varchar(150) NOT NULL,
    description text NOT NULL,
    service_type varchar(30) NOT NULL CONSTRAINT ck_survey_services_type CHECK (service_type IN ('PLANT_HEALTH', 'HARVEST_READINESS')),
    status varchar(20) NOT NULL CONSTRAINT ck_survey_services_status CHECK (status IN ('EXPERIMENTAL', 'ACTIVE', 'RETIRED')),
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_survey_services_version CHECK (version > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE survey.survey_service_prices (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    survey_service_id uuid NOT NULL REFERENCES survey.survey_services(id) ON DELETE RESTRICT,
    price_per_ha numeric(18,2) NOT NULL CONSTRAINT ck_survey_service_prices_amount_positive CHECK (price_per_ha > 0),
    currency char(3) NOT NULL CONSTRAINT ck_survey_service_prices_currency CHECK (currency ~ '^[A-Z]{3}$'),
    effective_from timestamptz NOT NULL,
    effective_to timestamptz,
    created_by uuid NOT NULL REFERENCES identity.users(id) ON DELETE RESTRICT,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_survey_service_prices_id_service UNIQUE (id, survey_service_id),
    CONSTRAINT ck_survey_service_prices_window CHECK (effective_to IS NULL OR effective_to > effective_from),
    CONSTRAINT ex_survey_service_prices_no_overlap EXCLUDE USING gist (
        survey_service_id WITH =,
        tstzrange(effective_from, effective_to, '[)') WITH &&
    )
);
CREATE INDEX ix_survey_service_prices_effective
    ON survey.survey_service_prices(survey_service_id, effective_from DESC);

CREATE TABLE survey.survey_requests (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    request_number varchar(40) NOT NULL CONSTRAINT uq_survey_requests_number UNIQUE,
    kind varchar(40) NOT NULL CONSTRAINT ck_survey_requests_kind CHECK (kind IN ('NEW_CUSTOMER', 'EXISTING_TENANT_NEW_FARM', 'EXISTING_FARM_SURVEY')),
    tenant_id uuid REFERENCES identity.tenants(id) ON DELETE RESTRICT,
    farm_id uuid,
    requested_by_user_id uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    survey_service_id uuid NOT NULL REFERENCES survey.survey_services(id) ON DELETE RESTRICT,
    caller_scope varchar(100) NOT NULL,
    idempotency_key varchar(100) NOT NULL,
    applicant_name varchar(150) NOT NULL,
    applicant_email varchar(320) NOT NULL,
    applicant_phone varchar(30) NOT NULL,
    farm_name varchar(200) NOT NULL,
    farm_address text NOT NULL,
    approximate_area_ha numeric(12,4) NOT NULL CONSTRAINT ck_survey_requests_area_positive CHECK (approximate_area_ha > 0),
    map_location geometry(Point,4326) NOT NULL,
    estimated_pole_count integer,
    preferred_start_at timestamptz,
    preferred_end_at timestamptz,
    notes text,
    status varchar(30) NOT NULL DEFAULT 'SUBMITTED'
        CONSTRAINT ck_survey_requests_status CHECK (status IN ('SUBMITTED', 'UNDER_REVIEW', 'APPROVED', 'REJECTED', 'WITHDRAWN')),
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_survey_requests_version CHECK (version > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_survey_requests_id_tenant_farm UNIQUE (id, tenant_id, farm_id),
    CONSTRAINT uq_survey_requests_caller_idempotency UNIQUE (caller_scope, idempotency_key),
    CONSTRAINT fk_survey_requests_farm_tenant FOREIGN KEY (farm_id, tenant_id) REFERENCES farm.farms(id, tenant_id) ON DELETE RESTRICT,
    CONSTRAINT ck_survey_requests_pole_count CHECK (estimated_pole_count IS NULL OR estimated_pole_count >= 0),
    CONSTRAINT ck_survey_requests_preferred_window CHECK (preferred_end_at IS NULL OR (preferred_start_at IS NOT NULL AND preferred_end_at > preferred_start_at)),
    CONSTRAINT ck_survey_requests_kind_context CHECK (
        (kind = 'NEW_CUSTOMER' AND tenant_id IS NULL AND farm_id IS NULL AND requested_by_user_id IS NULL)
        OR (kind = 'EXISTING_TENANT_NEW_FARM' AND tenant_id IS NOT NULL AND farm_id IS NULL AND requested_by_user_id IS NOT NULL)
        OR (kind = 'EXISTING_FARM_SURVEY' AND tenant_id IS NOT NULL AND farm_id IS NOT NULL AND requested_by_user_id IS NOT NULL))
);
CREATE INDEX ix_survey_requests_inbox ON survey.survey_requests(status, created_at);
CREATE INDEX ix_survey_requests_tenant_history ON survey.survey_requests(tenant_id, created_at DESC);
CREATE INDEX ix_survey_requests_map_location_gist ON survey.survey_requests USING gist(map_location);

CREATE TABLE survey.survey_request_reviews (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    survey_request_id uuid NOT NULL REFERENCES survey.survey_requests(id) ON DELETE RESTRICT,
    decision varchar(20) NOT NULL CONSTRAINT ck_survey_request_reviews_decision CHECK (decision IN ('APPROVED', 'REJECTED')),
    checklist_snapshot jsonb NOT NULL CONSTRAINT ck_survey_request_reviews_snapshot CHECK (jsonb_typeof(checklist_snapshot) = 'object'),
    reason text NOT NULL CONSTRAINT ck_survey_request_reviews_reason CHECK (length(btrim(reason)) > 0),
    reviewed_by uuid NOT NULL REFERENCES identity.users(id) ON DELETE RESTRICT,
    reviewed_at timestamptz NOT NULL
);
CREATE INDEX ix_survey_request_reviews_timeline ON survey.survey_request_reviews(survey_request_id, reviewed_at);

CREATE TABLE survey.survey_orders (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    order_number varchar(40) NOT NULL CONSTRAINT uq_survey_orders_number UNIQUE,
    tenant_id uuid NOT NULL REFERENCES identity.tenants(id) ON DELETE RESTRICT,
    farm_id uuid NOT NULL,
    survey_request_id uuid NOT NULL REFERENCES survey.survey_requests(id) ON DELETE RESTRICT,
    survey_service_id uuid NOT NULL REFERENCES survey.survey_services(id) ON DELETE RESTRICT,
    survey_service_price_id uuid,
    confirmed_survey_area_ha numeric(12,4),
    price_per_ha_snapshot numeric(18,2),
    currency char(3),
    final_price numeric(18,2),
    scope_confirmed_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    scope_confirmed_at timestamptz,
    requires_baseline_mapping boolean NOT NULL,
    previous_compatible_order_id uuid REFERENCES survey.survey_orders(id) ON DELETE RESTRICT,
    status varchar(40) NOT NULL DEFAULT 'PENDING_SCOPE_CONFIRMATION'
        CONSTRAINT ck_survey_orders_status CHECK (status IN ('PENDING_SCOPE_CONFIRMATION', 'AWAITING_APPOINTMENT', 'AWAITING_PAYMENT', 'READY_FOR_OPERATIONS', 'IN_PROGRESS', 'PENDING_REVIEW', 'COMPLETED', 'CANCELLED')),
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_survey_orders_version CHECK (version > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_survey_orders_id_tenant_farm UNIQUE (id, tenant_id, farm_id),
    CONSTRAINT uq_survey_orders_id_farm UNIQUE (id, farm_id),
    CONSTRAINT uq_survey_orders_request UNIQUE (survey_request_id),
    CONSTRAINT fk_survey_orders_farm_tenant FOREIGN KEY (farm_id, tenant_id) REFERENCES farm.farms(id, tenant_id) ON DELETE RESTRICT,
    CONSTRAINT fk_survey_orders_price_service FOREIGN KEY (survey_service_price_id, survey_service_id)
        REFERENCES survey.survey_service_prices(id, survey_service_id) ON DELETE RESTRICT,
    CONSTRAINT ck_survey_orders_scope_snapshot CHECK (
        (scope_confirmed_at IS NULL AND scope_confirmed_by IS NULL AND confirmed_survey_area_ha IS NULL AND price_per_ha_snapshot IS NULL AND currency IS NULL AND final_price IS NULL)
        OR (scope_confirmed_at IS NOT NULL AND scope_confirmed_by IS NOT NULL AND confirmed_survey_area_ha > 0 AND price_per_ha_snapshot > 0 AND currency ~ '^[A-Z]{3}$' AND final_price >= 0)),
    CONSTRAINT ck_survey_orders_money_scale CHECK (
        final_price IS NULL OR final_price = round(confirmed_survey_area_ha * price_per_ha_snapshot, 2))
);
CREATE INDEX ix_survey_orders_tenant_status ON survey.survey_orders(tenant_id, status, created_at DESC);
CREATE INDEX ix_survey_orders_farm_history ON survey.survey_orders(farm_id, created_at DESC);

CREATE TABLE survey.price_adjustments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    survey_order_id uuid NOT NULL REFERENCES survey.survey_orders(id) ON DELETE RESTRICT,
    old_area_ha numeric(12,4) NOT NULL,
    new_area_ha numeric(12,4) NOT NULL,
    old_price numeric(18,2) NOT NULL,
    new_price numeric(18,2) NOT NULL,
    reason text NOT NULL,
    status varchar(20) NOT NULL CONSTRAINT ck_price_adjustments_status CHECK (status IN ('PENDING', 'APPROVED', 'REJECTED', 'APPLIED')),
    requested_by uuid NOT NULL REFERENCES identity.users(id) ON DELETE RESTRICT,
    approved_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    approved_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_price_adjustments_area_positive CHECK (old_area_ha > 0 AND new_area_ha > 0),
    CONSTRAINT ck_price_adjustments_price_nonnegative CHECK (old_price >= 0 AND new_price >= 0),
    CONSTRAINT ck_price_adjustments_changed CHECK (old_area_ha <> new_area_ha OR old_price <> new_price),
    CONSTRAINT ck_price_adjustments_approval CHECK (
        (status IN ('APPROVED', 'APPLIED') AND approved_by IS NOT NULL AND approved_at IS NOT NULL)
        OR (status IN ('PENDING', 'REJECTED') AND approved_by IS NULL AND approved_at IS NULL))
);
CREATE INDEX ix_price_adjustments_order_created ON survey.price_adjustments(survey_order_id, created_at DESC);

CREATE TABLE survey.survey_appointments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    survey_order_id uuid NOT NULL REFERENCES survey.survey_orders(id) ON DELETE RESTRICT,
    proposed_start_at timestamptz NOT NULL,
    proposed_end_at timestamptz NOT NULL,
    status varchar(30) NOT NULL CONSTRAINT ck_survey_appointments_status CHECK (status IN ('PROPOSED', 'CONFIRMED', 'RESCHEDULE_REQUESTED', 'CANCELLED')),
    confirmed_by_tenant_owner_id uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    confirmed_at timestamptz,
    reschedule_reason text,
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_survey_appointments_version CHECK (version > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_survey_appointments_window CHECK (proposed_end_at > proposed_start_at),
    CONSTRAINT ck_survey_appointments_confirmation CHECK (
        (status = 'CONFIRMED' AND confirmed_by_tenant_owner_id IS NOT NULL AND confirmed_at IS NOT NULL)
        OR (status <> 'CONFIRMED' AND confirmed_at IS NULL))
);
CREATE UNIQUE INDEX uq_survey_appointments_active_order ON survey.survey_appointments(survey_order_id)
    WHERE status IN ('PROPOSED', 'CONFIRMED', 'RESCHEDULE_REQUESTED');

CREATE TABLE survey.survey_payments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    survey_order_id uuid NOT NULL REFERENCES survey.survey_orders(id) ON DELETE RESTRICT,
    amount numeric(18,2) NOT NULL CONSTRAINT ck_survey_payments_amount_positive CHECK (amount > 0),
    currency char(3) NOT NULL CONSTRAINT ck_survey_payments_currency CHECK (currency ~ '^[A-Z]{3}$'),
    provider varchar(50) NOT NULL,
    provider_reference varchar(150),
    status varchar(30) NOT NULL CONSTRAINT ck_survey_payments_status CHECK (status IN ('PENDING', 'PROCESSING', 'CONFIRMED', 'FAILED', 'REFUNDED', 'ADJUSTMENT_REQUIRED')),
    confirmed_at timestamptz,
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_survey_payments_version CHECK (version > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_survey_payments_confirmation CHECK (
        (status = 'CONFIRMED' AND confirmed_at IS NOT NULL AND provider_reference IS NOT NULL)
        OR status <> 'CONFIRMED')
);
CREATE UNIQUE INDEX uq_survey_payments_provider_reference ON survey.survey_payments(provider, provider_reference)
    WHERE provider_reference IS NOT NULL;
CREATE INDEX ix_survey_payments_order ON survey.survey_payments(survey_order_id, created_at DESC);

CREATE TABLE survey.payment_events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    survey_payment_id uuid NOT NULL REFERENCES survey.survey_payments(id) ON DELETE RESTRICT,
    provider varchar(50) NOT NULL,
    deduplication_key varchar(200) NOT NULL,
    event_type varchar(100) NOT NULL,
    payload jsonb NOT NULL,
    occurred_at timestamptz NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_payment_events_provider_dedup UNIQUE (provider, deduplication_key)
);

-- Official Farm base maps reference BE2 missions only by UUID, never by FK.
CREATE TABLE farm.farm_base_map_versions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    version_number integer NOT NULL CONSTRAINT ck_farm_base_map_versions_version_positive CHECK (version_number >= 1),
    source_survey_order_id uuid NOT NULL,
    source_mission_group_id uuid,
    status varchar(20) NOT NULL DEFAULT 'DRAFT'
        CONSTRAINT ck_farm_base_map_versions_status CHECK (status IN ('DRAFT', 'PUBLISHED', 'SUPERSEDED')),
    published_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    published_at timestamptz,
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_farm_base_map_versions_version CHECK (version > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_farm_base_map_versions_id_farm UNIQUE (id, farm_id),
    CONSTRAINT uq_farm_base_map_versions_id_tenant_farm UNIQUE (id, tenant_id, farm_id),
    CONSTRAINT uq_farm_base_map_versions_farm_version UNIQUE (farm_id, version_number),
    CONSTRAINT uq_farm_base_map_versions_source_order UNIQUE (source_survey_order_id),
    CONSTRAINT fk_farm_base_map_versions_farm_tenant FOREIGN KEY (farm_id, tenant_id) REFERENCES farm.farms(id, tenant_id) ON DELETE RESTRICT,
    CONSTRAINT fk_farm_base_map_versions_order_tenant_farm FOREIGN KEY (source_survey_order_id, tenant_id, farm_id)
        REFERENCES survey.survey_orders(id, tenant_id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT ck_farm_base_map_versions_publication CHECK (
        (status = 'DRAFT' AND published_by IS NULL AND published_at IS NULL)
        OR (status IN ('PUBLISHED', 'SUPERSEDED') AND published_by IS NOT NULL AND published_at IS NOT NULL))
);
CREATE UNIQUE INDEX uq_farm_base_map_versions_one_published
    ON farm.farm_base_map_versions(farm_id) WHERE status = 'PUBLISHED';

CREATE TABLE farm.zone_map_versions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    farm_id uuid NOT NULL,
    zone_id uuid NOT NULL,
    farm_base_map_version_id uuid NOT NULL,
    source_mission_id uuid,
    source_approval_id uuid,
    version_number integer NOT NULL CONSTRAINT ck_zone_map_versions_version_positive CHECK (version_number >= 1),
    status varchar(20) NOT NULL DEFAULT 'DRAFT' CONSTRAINT ck_zone_map_versions_status CHECK (status IN ('DRAFT', 'CONFIRMED', 'SUPERSEDED', 'REJECTED')),
    grid_bearing_deg numeric(6,2),
    row_spacing_m numeric(8,3),
    plant_spacing_m numeric(8,3),
    algorithm_version varchar(100),
    parameters jsonb NOT NULL DEFAULT '{}'::jsonb,
    confirmed_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    confirmed_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_zone_map_versions_id_farm UNIQUE (id, farm_id),
    CONSTRAINT uq_zone_map_versions_id_zone_farm UNIQUE (id, zone_id, farm_id),
    CONSTRAINT uq_zone_map_versions_zone_version UNIQUE (zone_id, version_number),
    CONSTRAINT fk_zone_map_versions_zone_farm FOREIGN KEY (zone_id, farm_id) REFERENCES farm.farm_zones(id, farm_id) ON DELETE CASCADE,
    CONSTRAINT fk_zone_map_versions_base_map_farm FOREIGN KEY (farm_base_map_version_id, farm_id)
        REFERENCES farm.farm_base_map_versions(id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT ck_zone_map_versions_bearing_range CHECK (grid_bearing_deg IS NULL OR (grid_bearing_deg >= 0 AND grid_bearing_deg < 360)),
    CONSTRAINT ck_zone_map_versions_spacing_positive CHECK ((row_spacing_m IS NULL OR row_spacing_m > 0) AND (plant_spacing_m IS NULL OR plant_spacing_m > 0)),
    CONSTRAINT ck_zone_map_versions_confirmation CHECK (
        (status IN ('CONFIRMED', 'SUPERSEDED') AND confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL)
        OR (status IN ('DRAFT', 'REJECTED') AND confirmed_by IS NULL AND confirmed_at IS NULL))
);
CREATE UNIQUE INDEX ux_zone_map_versions_one_confirmed ON farm.zone_map_versions(zone_id) WHERE status = 'CONFIRMED';
CREATE UNIQUE INDEX ux_zone_map_versions_source_approval ON farm.zone_map_versions(source_approval_id) WHERE source_approval_id IS NOT NULL;
CREATE INDEX ix_zone_map_versions_source_mission ON farm.zone_map_versions(source_mission_id);

-- Plant master data and official Digital Plant Profile.
CREATE TABLE plant.health_levels (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(30) NOT NULL CONSTRAINT uq_health_levels_code UNIQUE,
    name varchar(100) NOT NULL,
    rank integer,
    is_healthy boolean NOT NULL DEFAULT false,
    description text,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_health_levels_semantics CHECK (
        (code = 'UNKNOWN' AND rank IS NULL AND is_healthy = false)
        OR (code = 'HEALTHY' AND rank = 0 AND is_healthy = true)
        OR (code NOT IN ('UNKNOWN', 'HEALTHY') AND rank > 0 AND is_healthy = false))
);
CREATE UNIQUE INDEX uq_health_levels_rank ON plant.health_levels(rank) WHERE rank IS NOT NULL;

CREATE TABLE plant.plant_conditions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(50) NOT NULL,
    name varchar(150) NOT NULL,
    scientific_name varchar(150),
    condition_type varchar(30) NOT NULL DEFAULT 'DISEASE'
        CONSTRAINT ck_plant_conditions_type CHECK (condition_type IN ('DISEASE', 'ABIOTIC_DAMAGE', 'MECHANICAL_DAMAGE', 'OTHER')),
    description text,
    revision_number integer NOT NULL DEFAULT 1 CONSTRAINT ck_plant_conditions_revision CHECK (revision_number > 0),
    supersedes_id uuid REFERENCES plant.plant_conditions(id) ON DELETE RESTRICT,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    retired_at timestamptz,
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_plant_conditions_version CHECK (version > 0),
    CONSTRAINT uq_plant_conditions_code_revision UNIQUE (code, revision_number),
    CONSTRAINT ck_plant_conditions_retirement CHECK ((is_active AND retired_at IS NULL) OR (NOT is_active AND retired_at IS NOT NULL))
);
CREATE UNIQUE INDEX uq_plant_conditions_active_code ON plant.plant_conditions(code) WHERE is_active;
CREATE UNIQUE INDEX uq_plant_conditions_supersedes ON plant.plant_conditions(supersedes_id) WHERE supersedes_id IS NOT NULL;

CREATE TABLE plant.plants (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    farm_id uuid NOT NULL REFERENCES farm.farms(id) ON DELETE RESTRICT,
    zone_id uuid,
    plant_code varchar(50) NOT NULL,
    location geometry(Point,4326),
    current_map_version_id uuid,
    row_index integer,
    column_index integer,
    location_accuracy_m numeric(8,3),
    position_confidence numeric(5,4),
    position_source varchar(20) CONSTRAINT ck_plants_position_source CHECK (position_source IN ('MAPPING_AI', 'MANUAL', 'IMPORT')),
    lifecycle_status varchar(20) NOT NULL DEFAULT 'ACTIVE'
        CONSTRAINT ck_plants_lifecycle CHECK (lifecycle_status IN ('ACTIVE', 'MISSING', 'REMOVED', 'DEAD', 'INACTIVE')),
    current_health_level_id uuid NOT NULL REFERENCES plant.health_levels(id) ON DELETE RESTRICT,
    last_inspected_at timestamptz,
    mapped_at timestamptz,
    created_from_mission_id uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    retired_at timestamptz,
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_plants_version CHECK (version > 0),
    CONSTRAINT uq_plants_id_farm UNIQUE (id, farm_id),
    CONSTRAINT uq_plants_farm_code UNIQUE (farm_id, plant_code),
    CONSTRAINT fk_plants_zone_farm FOREIGN KEY (zone_id, farm_id) REFERENCES farm.farm_zones(id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT fk_plants_map_zone_farm FOREIGN KEY (current_map_version_id, zone_id, farm_id)
        REFERENCES farm.zone_map_versions(id, zone_id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT ck_plants_grid_position_complete CHECK (
        (current_map_version_id IS NULL AND row_index IS NULL AND column_index IS NULL)
        OR (current_map_version_id IS NOT NULL AND zone_id IS NOT NULL AND row_index IS NOT NULL AND column_index IS NOT NULL)),
    CONSTRAINT ck_plants_grid_indices_positive CHECK ((row_index IS NULL OR row_index >= 1) AND (column_index IS NULL OR column_index >= 1)),
    CONSTRAINT ck_plants_location_accuracy_nonnegative CHECK (location_accuracy_m IS NULL OR location_accuracy_m >= 0),
    CONSTRAINT ck_plants_position_confidence CHECK (position_confidence IS NULL OR position_confidence BETWEEN 0 AND 1)
);
CREATE INDEX ix_plants_farm ON plant.plants(farm_id);
CREATE INDEX ix_plants_zone ON plant.plants(zone_id);
CREATE INDEX ix_plants_location_gist ON plant.plants USING gist(location);
CREATE UNIQUE INDEX ux_plants_active_zone_grid_position ON plant.plants(zone_id, row_index, column_index)
    WHERE row_index IS NOT NULL AND column_index IS NOT NULL AND lifecycle_status IN ('ACTIVE', 'MISSING');

CREATE TABLE plant.plant_change_events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    farm_id uuid NOT NULL,
    mission_id uuid,
    plant_id uuid NOT NULL,
    change_type varchar(30) NOT NULL CONSTRAINT ck_plant_change_events_type CHECK (change_type IN ('NEW_PLANT', 'MISSING_PLANT', 'REMOVED_PLANT', 'DEAD_PLANT', 'DETECTION_ERROR', 'MAPPING_DIFFERENCE')),
    source varchar(20) NOT NULL DEFAULT 'MISSION_AI' CONSTRAINT ck_plant_change_events_source CHECK (source IN ('MISSION_AI', 'MANUAL')),
    old_location geometry(Point,4326),
    new_location geometry(Point,4326),
    old_row_index integer,
    new_row_index integer,
    old_column_index integer,
    new_column_index integer,
    old_lifecycle_status varchar(20),
    new_lifecycle_status varchar(20),
    created_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    status varchar(20) NOT NULL DEFAULT 'PENDING' CONSTRAINT ck_plant_change_events_status CHECK (status IN ('PENDING', 'CONFIRMED', 'REJECTED')),
    notes text,
    reviewed_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    reviewed_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_change_event_plant_same_farm FOREIGN KEY (plant_id, farm_id) REFERENCES plant.plants(id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT ck_plant_change_source_actor CHECK ((source = 'MISSION_AI' AND mission_id IS NOT NULL) OR (source = 'MANUAL' AND created_by IS NOT NULL)),
    CONSTRAINT ck_plant_change_has_difference CHECK (
        old_location IS DISTINCT FROM new_location OR old_row_index IS DISTINCT FROM new_row_index
        OR old_column_index IS DISTINCT FROM new_column_index OR old_lifecycle_status IS DISTINCT FROM new_lifecycle_status),
    CONSTRAINT ck_plant_change_review CHECK (
        (status = 'PENDING' AND reviewed_by IS NULL AND reviewed_at IS NULL)
        OR (status IN ('CONFIRMED', 'REJECTED') AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL))
);
CREATE INDEX ix_plant_change_events_plant ON plant.plant_change_events(plant_id);
CREATE INDEX ix_plant_change_events_farm_created ON plant.plant_change_events(farm_id, created_at DESC);

CREATE TABLE survey.survey_results (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    survey_order_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    service_type varchar(30) NOT NULL CONSTRAINT ck_survey_results_service_type CHECK (service_type IN ('PLANT_HEALTH', 'HARVEST_READINESS')),
    status varchar(20) NOT NULL DEFAULT 'PENDING_REVIEW' CONSTRAINT ck_survey_results_status CHECK (status IN ('PENDING_REVIEW', 'APPROVED', 'PUBLISHED')),
    provenance jsonb NOT NULL DEFAULT '{}'::jsonb,
    reviewed_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    reviewed_at timestamptz,
    published_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    published_at timestamptz,
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_survey_results_version CHECK (version > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_survey_results_id_farm UNIQUE (id, farm_id),
    CONSTRAINT uq_survey_results_id_order_farm UNIQUE (id, survey_order_id, farm_id),
    CONSTRAINT uq_survey_results_order UNIQUE (survey_order_id),
    CONSTRAINT fk_survey_results_order_tenant_farm FOREIGN KEY (survey_order_id, tenant_id, farm_id)
        REFERENCES survey.survey_orders(id, tenant_id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT ck_survey_results_review_publication CHECK (
        (status = 'PENDING_REVIEW' AND reviewed_by IS NULL AND reviewed_at IS NULL AND published_by IS NULL AND published_at IS NULL)
        OR (status = 'APPROVED' AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND published_by IS NULL AND published_at IS NULL)
        OR (status = 'PUBLISHED' AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL))
);
CREATE INDEX ix_survey_results_farm_status ON survey.survey_results(tenant_id, farm_id, status);
CREATE INDEX ix_survey_results_profile_timeline ON survey.survey_results(farm_id, service_type, published_at DESC);

CREATE TABLE survey.harvest_readiness_assessments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    survey_result_id uuid NOT NULL,
    survey_order_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    plant_id uuid,
    mission_id uuid,
    assessment_granularity varchar(50) NOT NULL,
    criteria_version varchar(100) NOT NULL,
    visible_indicators jsonb NOT NULL,
    ai_assessment varchar(100) NOT NULL,
    ai_confidence numeric(5,4),
    corrected_assessment varchar(100),
    evidence jsonb NOT NULL DEFAULT '{}'::jsonb,
    status varchar(20) NOT NULL DEFAULT 'PENDING' CONSTRAINT ck_harvest_readiness_status CHECK (status IN ('PENDING', 'REVIEWED', 'REJECTED')),
    reviewed_by uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    reviewed_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_harvest_readiness_result_order_farm FOREIGN KEY (survey_result_id, survey_order_id, farm_id)
        REFERENCES survey.survey_results(id, survey_order_id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT fk_harvest_readiness_plant_farm FOREIGN KEY (plant_id, farm_id) REFERENCES plant.plants(id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT ck_harvest_readiness_confidence CHECK (ai_confidence IS NULL OR ai_confidence BETWEEN 0 AND 1),
    CONSTRAINT ck_harvest_readiness_review CHECK (
        (status = 'PENDING' AND reviewed_by IS NULL AND reviewed_at IS NULL)
        OR (status <> 'PENDING' AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL))
);
CREATE INDEX ix_harvest_readiness_result ON survey.harvest_readiness_assessments(survey_result_id);
CREATE INDEX ix_harvest_readiness_mission ON survey.harvest_readiness_assessments(mission_id);

CREATE TABLE notification.notifications (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid REFERENCES identity.users(id) ON DELETE RESTRICT,
    tenant_id uuid REFERENCES identity.tenants(id) ON DELETE RESTRICT,
    farm_id uuid REFERENCES farm.farms(id) ON DELETE RESTRICT,
    notification_type varchar(50) NOT NULL,
    title varchar(200) NOT NULL,
    message text NOT NULL,
    entity_type varchar(50),
    entity_id uuid,
    is_read boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    read_at timestamptz,
    CONSTRAINT ck_notification_read_time CHECK ((NOT is_read AND read_at IS NULL) OR (is_read AND read_at IS NOT NULL)),
    CONSTRAINT ck_notification_farm_tenant_context CHECK (farm_id IS NULL OR tenant_id IS NOT NULL)
);
CREATE INDEX ix_notifications_user_unread ON notification.notifications(user_id, created_at DESC) WHERE NOT is_read;
CREATE INDEX ix_notifications_tenant_created ON notification.notifications(tenant_id, created_at DESC);
CREATE INDEX ix_notifications_farm_created ON notification.notifications(farm_id, created_at DESC);

CREATE TABLE audit.audit_logs (
    id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    user_id uuid,
    tenant_id uuid,
    farm_id uuid,
    actor_type varchar(20) NOT NULL DEFAULT 'SYSTEM' CONSTRAINT ck_audit_actor_type CHECK (actor_type IN ('USER', 'AI', 'SYSTEM')),
    actor_id uuid,
    correlation_id uuid,
    source_job_id uuid,
    entity_type varchar(100) NOT NULL,
    entity_id uuid,
    action varchar(50) NOT NULL,
    old_data jsonb,
    new_data jsonb,
    reason text,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_audit_actor_context CHECK (
        (actor_type = 'USER' AND coalesce(actor_id, user_id) IS NOT NULL)
        OR (actor_type = 'AI' AND source_job_id IS NOT NULL) OR actor_type = 'SYSTEM'),
    CONSTRAINT ck_audit_farm_tenant_context CHECK (farm_id IS NULL OR tenant_id IS NOT NULL)
);
CREATE INDEX ix_audit_logs_entity ON audit.audit_logs(entity_type, entity_id, created_at DESC);
CREATE INDEX ix_audit_logs_farm ON audit.audit_logs(farm_id, created_at DESC);
CREATE INDEX ix_audit_logs_tenant ON audit.audit_logs(tenant_id, created_at DESC);
CREATE INDEX ix_audit_logs_correlation ON audit.audit_logs(correlation_id);

CREATE OR REPLACE FUNCTION audit.reject_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'audit logs are append-only' USING ERRCODE = '55000';
END $$;
CREATE TRIGGER trg_audit_logs_append_only
    BEFORE UPDATE OR DELETE ON audit.audit_logs FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();

CREATE TABLE messaging.inbox_messages (
    consumer_name varchar(150) NOT NULL,
    message_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    correlation_id uuid NOT NULL,
    event_type varchar(200) NOT NULL,
    schema_version integer NOT NULL CONSTRAINT ck_inbox_messages_schema_version CHECK (schema_version > 0),
    status varchar(30) NOT NULL CONSTRAINT ck_inbox_messages_status CHECK (status IN ('PROCESSING', 'COMPLETED', 'FAILED')),
    received_at timestamptz NOT NULL,
    completed_at timestamptz,
    result jsonb,
    error_code varchar(100),
    last_error varchar(2000),
    CONSTRAINT pk_inbox_messages PRIMARY KEY (consumer_name, message_id),
    CONSTRAINT ck_inbox_messages_completion CHECK (
        (status = 'PROCESSING' AND completed_at IS NULL)
        OR (status IN ('COMPLETED', 'FAILED') AND completed_at IS NOT NULL AND completed_at >= received_at)),
    CONSTRAINT ck_inbox_messages_result CHECK (result IS NULL OR status = 'COMPLETED'),
    CONSTRAINT ck_inbox_messages_error CHECK (
        (status = 'FAILED' AND error_code IS NOT NULL)
        OR (status <> 'FAILED' AND error_code IS NULL AND last_error IS NULL))
);
CREATE INDEX ix_inbox_messages_status_received_at ON messaging.inbox_messages(status, received_at);
CREATE INDEX ix_inbox_messages_tenant_correlation ON messaging.inbox_messages(tenant_id, correlation_id);

CREATE TABLE messaging.outbox_messages (
    message_id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    correlation_id uuid NOT NULL,
    actor_id uuid,
    event_type varchar(200) NOT NULL,
    schema_version integer NOT NULL CONSTRAINT ck_outbox_messages_schema_version CHECK (schema_version > 0),
    routing_key varchar(200) NOT NULL,
    body bytea NOT NULL CONSTRAINT ck_outbox_messages_body_size CHECK (octet_length(body) BETWEEN 1 AND 4194304),
    content_type varchar(100) NOT NULL,
    partition_key varchar(200),
    status varchar(30) NOT NULL CONSTRAINT ck_outbox_messages_status CHECK (status IN ('PENDING', 'PROCESSING', 'RETRY', 'PUBLISHED', 'DEAD')),
    attempt_count integer NOT NULL DEFAULT 0 CONSTRAINT ck_outbox_messages_attempt_count CHECK (attempt_count >= 0),
    next_attempt_at timestamptz,
    locked_by uuid,
    locked_until timestamptz,
    occurred_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL,
    published_at timestamptz,
    last_error varchar(2000),
    version bigint NOT NULL DEFAULT 1 CONSTRAINT ck_outbox_messages_version CHECK (version > 0),
    CONSTRAINT ck_outbox_messages_processing_lease CHECK (
        (status = 'PROCESSING' AND locked_by IS NOT NULL AND locked_until IS NOT NULL)
        OR (status <> 'PROCESSING' AND locked_by IS NULL AND locked_until IS NULL)),
    CONSTRAINT ck_outbox_messages_publication CHECK (
        (status = 'PUBLISHED' AND published_at IS NOT NULL) OR (status <> 'PUBLISHED' AND published_at IS NULL)),
    CONSTRAINT ck_outbox_messages_retry_schedule CHECK (
        (status IN ('PENDING', 'RETRY') AND next_attempt_at IS NOT NULL)
        OR (status NOT IN ('PENDING', 'RETRY') AND next_attempt_at IS NULL)),
    CONSTRAINT ck_outbox_messages_timestamps CHECK (created_at >= occurred_at AND (published_at IS NULL OR published_at >= created_at))
);
CREATE INDEX ix_outbox_messages_dispatch ON messaging.outbox_messages(status, next_attempt_at, occurred_at)
    WHERE status IN ('PENDING', 'RETRY');
CREATE INDEX ix_outbox_messages_lease ON messaging.outbox_messages(status, locked_until) WHERE status = 'PROCESSING';
CREATE INDEX ix_outbox_messages_tenant_correlation ON messaging.outbox_messages(tenant_id, correlation_id);
CREATE INDEX ix_outbox_messages_partition ON messaging.outbox_messages(partition_key, occurred_at) WHERE partition_key IS NOT NULL;

-- Stable business seeds. IDs are deterministic so contracts and future
-- migrations can reference them without lookup-by-label races.
INSERT INTO identity.roles(id, code, name, description, created_at) VALUES
    ('00000000-0000-0000-0000-000000000001', 'SYSTEM_ADMIN', 'System Administrator', 'Administrator with system-wide access.', '2026-09-25T00:00:00Z'),
    ('00000000-0000-0000-0000-000000000002', 'SYSTEM_MANAGER', 'System Manager', 'AgriDrone operations manager assigned to customer farms.', '2026-09-25T00:00:00Z');

INSERT INTO identity.initialization_locks(name, version) VALUES ('system-admin-bootstrap', 0);

INSERT INTO survey.survey_services(id, code, name, description, service_type, status, created_at, updated_at) VALUES
    ('10000000-0000-0000-0000-000000000001', 'PLANT_HEALTH', 'Plant Health Survey', 'Drone imagery survey for human-verified plant health findings.', 'PLANT_HEALTH', 'ACTIVE', '2026-09-25T00:00:00Z', '2026-09-25T00:00:00Z'),
    ('10000000-0000-0000-0000-000000000002', 'HARVEST_READINESS', 'Harvest Readiness Survey', 'Experimental visible-indicator assessment of harvest readiness.', 'HARVEST_READINESS', 'EXPERIMENTAL', '2026-09-25T00:00:00Z', '2026-09-25T00:00:00Z');

INSERT INTO plant.health_levels(id, code, name, rank, is_healthy, description, is_active, created_at, updated_at) VALUES
    ('11111111-1111-4111-8111-111111111101', 'UNKNOWN', 'Unknown', NULL, false, 'Health has not been assessed.', true, '2026-09-25T00:00:00Z', '2026-09-25T00:00:00Z'),
    ('11111111-1111-4111-8111-111111111102', 'HEALTHY', 'Healthy', 0, true, 'No material health condition detected.', true, '2026-09-25T00:00:00Z', '2026-09-25T00:00:00Z'),
    ('11111111-1111-4111-8111-111111111103', 'MILD', 'Mild', 1, false, 'Low-severity condition.', true, '2026-09-25T00:00:00Z', '2026-09-25T00:00:00Z'),
    ('11111111-1111-4111-8111-111111111104', 'MODERATE', 'Moderate', 2, false, 'Moderate-severity condition.', true, '2026-09-25T00:00:00Z', '2026-09-25T00:00:00Z'),
    ('11111111-1111-4111-8111-111111111105', 'SEVERE', 'Severe', 3, false, 'High-severity condition.', true, '2026-09-25T00:00:00Z', '2026-09-25T00:00:00Z');

INSERT INTO plant.plant_conditions
    (id, code, name, condition_type, description, revision_number, is_active, created_at, updated_at)
VALUES
    ('30000000-0000-0000-0000-000000000001', 'BROWN_SPOT', 'Brown Spot', 'DISEASE', 'Approved baseline disease catalogue entry.', 1, true, '2026-09-25T00:00:00Z', '2026-09-25T00:00:00Z'),
    ('30000000-0000-0000-0000-000000000002', 'ANTHRACNOSE', 'Anthracnose', 'DISEASE', 'Approved baseline disease catalogue entry.', 1, true, '2026-09-25T00:00:00Z', '2026-09-25T00:00:00Z'),
    ('30000000-0000-0000-0000-000000000003', 'SUNBURN', 'Sunburn', 'ABIOTIC_DAMAGE', 'Approved baseline abiotic-damage catalogue entry.', 1, true, '2026-09-25T00:00:00Z', '2026-09-25T00:00:00Z'),
    ('30000000-0000-0000-0000-000000000004', 'MECHANICAL_SCAR', 'Mechanical Scar', 'MECHANICAL_DAMAGE', 'Approved baseline mechanical-damage catalogue entry.', 1, true, '2026-09-25T00:00:00Z', '2026-09-25T00:00:00Z');

-- Keep every application object private to the database owner. Compose creates
-- BE1 and BE2 as different database owners, so neither service can write the
-- other service's database.
REVOKE ALL ON ALL TABLES IN SCHEMA identity, farm, plant, survey, notification, audit, messaging FROM PUBLIC;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA identity, farm, plant, survey, notification, audit, messaging FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA identity, farm, plant, survey, notification, audit, messaging FROM PUBLIC;
