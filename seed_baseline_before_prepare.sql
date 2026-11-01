-- MOCK TEST DATA: actors and baseline survey order ready for mission/prepare.
-- Run only against a development/test database. Password for both users: Test@123456
CREATE EXTENSION IF NOT EXISTS pgcrypto;

DO $$
DECLARE
    v_now timestamptz := clock_timestamp();
    v_suffix text := upper(left(replace(gen_random_uuid()::text, '-', ''), 10));
    v_tenant_id uuid := gen_random_uuid();
    v_owner_id uuid := gen_random_uuid();
    v_manager_id uuid := gen_random_uuid();
    v_manager_profile_id uuid := gen_random_uuid();
    v_assignment_id uuid := gen_random_uuid();
    v_farm_id uuid := gen_random_uuid();
    v_boundary_id uuid := gen_random_uuid();
    v_drone_id uuid := gen_random_uuid();
    v_request_id uuid := gen_random_uuid();
    v_order_id uuid := gen_random_uuid();
    v_appointment_id uuid := gen_random_uuid();
    v_payment_id uuid := gen_random_uuid();
    v_consent_id uuid := gen_random_uuid();
    v_permission_id uuid := gen_random_uuid();
    v_manager_role_id uuid;
    v_service_id uuid;
    v_start_at timestamptz;
    v_end_at timestamptz;
    v_boundary geometry := ST_GeomFromText(
        'POLYGON((106.4941327 10.4618749,106.4940086 10.4615431,106.4939875 10.4615490,106.4939962 10.4615648,106.4939597 10.4615784,106.4939362 10.4615889,106.4939463 10.4616206,106.4940576 10.4619000,106.4941327 10.4618749))',
        4326);
    v_owner_email text := 'owner-' || lower(v_suffix) || '@example.test';
    v_manager_email text := 'manager-' || lower(v_suffix) || '@example.test';
    v_owner_phone text := '09' || lpad(floor(random() * 100000000)::bigint::text, 8, '0');
    v_manager_phone text := '08' || lpad(floor(random() * 100000000)::bigint::text, 8, '0');
BEGIN
    -- Schedule a three-hour baseline appointment at 08:00 Vietnam time,
    -- three days from the current Vietnam calendar date.
    v_start_at := ((date_trunc('day', v_now AT TIME ZONE 'Asia/Ho_Chi_Minh')
                    + interval '3 days' + interval '8 hours')
                   AT TIME ZONE 'Asia/Ho_Chi_Minh');
    v_end_at := v_start_at + interval '3 hours';

    INSERT INTO identity.roles (code, name)
    VALUES ('SYSTEM_MANAGER', 'System Manager')
    ON CONFLICT (code) DO NOTHING;

    SELECT id INTO STRICT v_manager_role_id
    FROM identity.roles WHERE code = 'SYSTEM_MANAGER';

    SELECT id INTO STRICT v_service_id
    FROM survey.survey_services WHERE code = 'PLANT_HEALTH';

    INSERT INTO identity.tenants (id, code, name, status, created_at, updated_at)
    VALUES (v_tenant_id, 'BASE-' || v_suffix, 'Baseline Test Tenant ' || v_suffix,
            'ACTIVE'::system.general_status, v_now, v_now);

    INSERT INTO identity.users
        (id, email, password_hash, full_name, phone, status, created_at, updated_at)
    VALUES
        (v_owner_id, v_owner_email, crypt('Test@123456', gen_salt('bf', 12)),
         'Baseline Test Tenant Owner', v_owner_phone, 'ACTIVE'::system.user_status, v_now, v_now),
        (v_manager_id, v_manager_email, crypt('Test@123456', gen_salt('bf', 12)),
         'Baseline Test System Manager', v_manager_phone, 'ACTIVE'::system.user_status, v_now, v_now);

    INSERT INTO identity.tenant_memberships
        (id, tenant_id, user_id, role, status, joined_at, created_at)
    VALUES (gen_random_uuid(), v_tenant_id, v_owner_id,
            'OWNER'::system.tenant_member_role, 'ACTIVE'::system.general_status, v_now, v_now);

    INSERT INTO identity.user_roles (user_id, role_id, assigned_at)
    VALUES (v_manager_id, v_manager_role_id, v_now);

    INSERT INTO identity.system_manager_profiles
        (id, user_id, status, availability, qualification_status,
         qualification_expires_at, created_at, updated_at, version)
    VALUES (v_manager_profile_id, v_manager_id,
            'ACTIVE'::system.system_manager_profile_status,
            'AVAILABLE'::system.system_manager_availability_status,
            'QUALIFIED'::system.flight_qualification_status,
            v_now + interval '1 year', v_now, v_now, 1);

    INSERT INTO farm.farms
        (id, tenant_id, code, name, address, boundary, center_point, area_hectares,
         status, created_by, created_at, updated_at, version)
    VALUES (v_farm_id, v_tenant_id, 'FARM-' || v_suffix,
            'Baseline Test Dragon Fruit Farm', 'Long An, Vietnam', v_boundary,
            ST_SetSRID(ST_MakePoint(106.49405, 10.46165), 4326), 1.0000,
            'ACTIVE'::system.general_status, v_owner_id, v_now, v_now, 1);

    INSERT INTO identity.farm_manager_assignments
        (id, tenant_id, farm_id, system_manager_profile_id, assigned_by,
         assignment_reason, assigned_at, version)
    VALUES (v_assignment_id, v_tenant_id, v_farm_id, v_manager_profile_id,
            v_owner_id, 'Mock assignment for baseline mission preparation', v_now, 1);

    INSERT INTO mission.drones
        (id, code, name, model, manufacturer, specifications, serial_number,
         registration_number, registration_date, registration_expiry_date,
         weight_kg, status, last_maintenance_at, next_maintenance_at,
         notes, created_at, updated_at)
    VALUES (v_drone_id, 'DR-' || v_suffix, 'Baseline Test Drone', 'Matrice 350 RTK',
            'DJI', jsonb_build_object('capabilities',
                jsonb_build_array('BASELINE_MAPPING', 'PLANT_HEALTH')),
            'SN-' || v_suffix, 'REG-' || v_suffix,
            current_date - 30, current_date + 335, 6.500,
            'AVAILABLE'::system.drone_status,
            v_now - interval '7 days', v_now + interval '6 months',
            'Mock drone ready for baseline prepare test', v_now, v_now);

    INSERT INTO survey.survey_requests
        (id, request_number, kind, tenant_id, farm_id, requested_by_user_id,
         survey_service_id, caller_scope, idempotency_key, applicant_name,
         applicant_email, applicant_phone, farm_name, farm_address,
         approximate_area_ha, map_location, estimated_pole_count,
         preferred_start_at, preferred_end_at, notes, status, created_at, updated_at)
    VALUES (v_request_id, 'REQ-' || v_suffix,
            'EXISTING_FARM_SURVEY'::system.survey_request_kind,
            v_tenant_id, v_farm_id, v_owner_id, v_service_id, 'TENANT_OWNER',
            'BASELINE-PREPARE-' || v_suffix,
            'Baseline Test Tenant Owner', v_owner_email, v_owner_phone,
            'Baseline Test Dragon Fruit Farm', 'Long An, Vietnam', 1.0000,
            ST_SetSRID(ST_MakePoint(106.49405, 10.46165), 4326), 61,
            v_start_at, v_end_at,
            'Mock baseline order ready immediately before prepare',
            'APPROVED'::system.survey_request_status, v_now, v_now);

    INSERT INTO farm.farm_boundaries
        (id, tenant_id, farm_id, version_number, geometry, source,
         source_survey_request_id, submitted_by, evidence_reference, status,
         reviewed_by, reviewed_at, review_reason, created_at, updated_at)
    VALUES (v_boundary_id, v_tenant_id, v_farm_id, 1, v_boundary,
            'SYSTEM_MANAGER'::system.farm_boundary_source, v_request_id,
            v_manager_id, 'seed://baseline-prepare-test',
            'APPROVED'::system.farm_boundary_status, v_manager_id, v_now,
            'Mock approved boundary to satisfy mission planning context', v_now, v_now);

    INSERT INTO survey.survey_orders
        (id, order_number, tenant_id, farm_id, survey_request_id, survey_service_id,
         farm_boundary_version_id, scope_confirmed_by, scope_confirmed_at,
         requires_baseline_mapping, deposit_amount, deposit_payment_status,
         deposit_confirmed_at, scope_type, status, created_at, updated_at)
    VALUES (v_order_id, 'ORD-' || v_suffix, v_tenant_id, v_farm_id,
            v_request_id, v_service_id, v_boundary_id, v_owner_id, v_now,
            true, 10000.00, 'PAID'::system.survey_payment_status, v_now,
            'FARM_WIDE'::system.survey_scope_type,
            'BASELINE_READY'::system.survey_order_status, v_now, v_now);

    INSERT INTO survey.survey_payments
        (id, survey_order_id, amount, currency, provider, provider_reference,
         status, purpose, confirmed_at, created_at, updated_at)
    VALUES (v_payment_id, v_order_id, 10000.00, 'VND', 'LOCAL_TEST',
            'BASELINE-DEPOSIT-' || v_suffix,
            'PAID'::system.survey_payment_status,
            'BaselineDeposit',
            v_now, v_now, v_now);

    UPDATE survey.survey_orders
    SET deposit_payment_id = v_payment_id
    WHERE id = v_order_id;

    INSERT INTO survey.survey_appointments
        (id, survey_order_id, purpose, revision_number,
         proposed_by_system_manager_id, proposed_start_at, proposed_end_at,
         status, confirmed_by_tenant_owner_id, confirmed_at, created_at, updated_at)
    VALUES (v_appointment_id, v_order_id,
            'BASELINE_MAPPING'::system.survey_appointment_purpose, 1,
            v_manager_id, v_start_at, v_end_at,
            'CONFIRMED'::system.survey_appointment_status,
            v_owner_id, v_now, v_now, v_now);

    INSERT INTO survey.survey_capture_authorizations
        (id, survey_order_id, authorization_type, status, evidence_reference,
         issued_at, verified_at, expires_at, verified_by, created_at, updated_at)
    VALUES
        (v_consent_id, v_order_id,
         'WRITTEN_DATA_USE_CONSENT'::system.survey_capture_authorization_type,
         'VERIFIED'::system.survey_capture_authorization_status,
         'seed://mock-written-data-use-consent', v_now, v_now,
         v_now + interval '1 year', v_manager_id, v_now, v_now),
        (v_permission_id, v_order_id,
         'FLIGHT_PERMISSION'::system.survey_capture_authorization_type,
         'VERIFIED'::system.survey_capture_authorization_status,
         'seed://mock-flight-permission', v_now, v_now,
         v_now + interval '1 year', v_manager_id, v_now, v_now);

    RAISE NOTICE 'Tenant ID         : %', v_tenant_id;
    RAISE NOTICE 'Tenant Owner ID   : %', v_owner_id;
    RAISE NOTICE 'Owner email       : %', v_owner_email;
    RAISE NOTICE 'Owner password    : Test@123456';
    RAISE NOTICE 'Manager ID        : %', v_manager_id;
    RAISE NOTICE 'Manager email     : %', v_manager_email;
    RAISE NOTICE 'Manager password  : Test@123456';
    RAISE NOTICE 'Farm ID           : %', v_farm_id;
    RAISE NOTICE 'Boundary ID       : %', v_boundary_id;
    RAISE NOTICE 'Drone ID          : %', v_drone_id;
    RAISE NOTICE 'Survey Request ID : %', v_request_id;
    RAISE NOTICE 'Survey Order ID   : %', v_order_id;
    RAISE NOTICE 'Appointment ID    : %', v_appointment_id;
    RAISE NOTICE 'Deposit Payment ID: %', v_payment_id;
    RAISE NOTICE 'Order status      : BASELINE_READY';
    RAISE NOTICE 'Mission rows      : NONE (call prepare next)';
    RAISE NOTICE 'Baseline date     : %', (v_start_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;
END $$;
