DO $$
DECLARE
    v_suffix text := upper(left(replace(gen_random_uuid()::text, '-', ''), 10));
    v_tenant uuid := gen_random_uuid();
    v_admin uuid := gen_random_uuid();
    v_manager uuid := gen_random_uuid();
    v_profile uuid := gen_random_uuid();
    v_farm uuid := gen_random_uuid();
    v_drone uuid := gen_random_uuid();
    v_service uuid := gen_random_uuid();
    v_request uuid := gen_random_uuid();
    v_order uuid := gen_random_uuid();
    v_mission uuid := gen_random_uuid();
    v_admin_role uuid;
    v_manager_role uuid;

    -- Thời gian bắt đầu: 01/10/2026 00:00:00 UTC
    v_started_at timestamptz :=
        TIMESTAMPTZ '2026-10-01 00:00:00+00:00';

    -- Thời gian kết thúc: 30/11/2026 23:59:59 UTC
    v_ended_at timestamptz :=
        TIMESTAMPTZ '2026-11-30 23:59:59+00:00';

BEGIN
    -- 1. Tạo roles
    INSERT INTO identity.roles (code, name)
    VALUES
        ('SYSTEM_ADMIN', 'System Admin'),
        ('SYSTEM_MANAGER', 'System Manager')
    ON CONFLICT (code) DO NOTHING;

    SELECT id INTO STRICT v_admin_role
    FROM identity.roles
    WHERE code = 'SYSTEM_ADMIN';

    SELECT id INTO STRICT v_manager_role
    FROM identity.roles
    WHERE code = 'SYSTEM_MANAGER';

    -- 2. Tạo tenant
    INSERT INTO identity.tenants (id, code, name)
    VALUES (
        v_tenant,
        'UC04-' || v_suffix,
        'UC04 Test ' || v_suffix
    );

    -- 3. Tạo users
    INSERT INTO identity.users
        (id, email, password_hash, full_name, status)
    VALUES
        (
            v_admin,
            'uc04-admin-' || lower(v_suffix) || '@example.test',
            crypt('CHANGE_ME_PASSWORD', gen_salt('bf', 12)),
            'UC04 Test Admin',
            'ACTIVE'::system.user_status
        ),
        (
            v_manager,
            'uc04-manager-' || lower(v_suffix) || '@example.test',
            crypt('CHANGE_ME_PASSWORD', gen_salt('bf', 12)),
            'UC04 Test Manager',
            'ACTIVE'::system.user_status
        );

    -- 4. Gán roles
    INSERT INTO identity.user_roles (user_id, role_id)
    VALUES
        (v_admin, v_admin_role),
        (v_manager, v_manager_role);

    -- 5. Tạo system manager profile
    INSERT INTO identity.system_manager_profiles (
        id,
        user_id,
        status,
        availability,
        qualification_status,
        qualification_expires_at,
        created_at,
        updated_at,
        version
    )
    VALUES (
        v_profile,
        v_manager,
        'ACTIVE'::system.system_manager_profile_status,
        'AVAILABLE'::system.system_manager_availability_status,
        'QUALIFIED'::system.flight_qualification_status,
        now() + interval '1 year',
        now(),
        now(),
        1
    );

    -- 6. Tạo drone
    INSERT INTO mission.drones (
        id, code, name, status
    )
    VALUES (
        v_drone,
        'DR-' || v_suffix,
        'UC04 Test Drone',
        'AVAILABLE'::system.drone_status
    );

    -- 7. Tạo survey service
    INSERT INTO survey.survey_services (
        id,
        code,
        name,
        description,
        service_type,
        status
    )
    VALUES (
        v_service,
        'SVC-' || v_suffix,
        'UC04 Test Service',
        'Local upload test service',
        'PLANT_HEALTH'::system.survey_service_type,
        'ACTIVE'::system.survey_service_status
    );

    -- 8. Tạo farm
    INSERT INTO farm.farms (
        id,
        tenant_id,
        code,
        name,
        created_by
    )
    VALUES (
        v_farm,
        v_tenant,
        'FARM-' || v_suffix,
        'UC04 Test Farm',
        v_admin
    );

    -- 9. Gán manager cho farm
    INSERT INTO identity.farm_manager_assignments (
        id,
        tenant_id,
        farm_id,
        system_manager_profile_id,
        assigned_by,
        assignment_reason,
        assigned_at
    )
    VALUES (
        gen_random_uuid(),
        v_tenant,
        v_farm,
        v_profile,
        v_admin,
        'Local UC04 upload test',
        now()
    );

    -- 10. Tạo survey request
    INSERT INTO survey.survey_requests (
        id,
        request_number,
        kind,
        tenant_id,
        farm_id,
        requested_by_user_id,
        survey_service_id,
        caller_scope,
        idempotency_key,
        applicant_name,
        applicant_email,
        applicant_phone,
        farm_name,
        farm_address,
        approximate_area_ha,
        map_location,
        status
    )
    VALUES (
        v_request,
        'REQ-' || v_suffix,
        'EXISTING_FARM_SURVEY'::system.survey_request_kind,
        v_tenant,
        v_farm,
        v_manager,
        v_service,
        'UC04-LOCAL-' || v_suffix,
        gen_random_uuid()::text,
        'UC04 Test Manager',
        'uc04-manager-' || lower(v_suffix) || '@example.test',
        '0900000000',
        'UC04 Test Farm',
        'Local test address',
        1,
        ST_SetSRID(ST_MakePoint(106.7, 10.8), 4326),
        'APPROVED'::system.survey_request_status
    );

    -- 11. Tạo survey order
    INSERT INTO survey.survey_orders (
        id,
        order_number,
        tenant_id,
        farm_id,
        survey_request_id,
        survey_service_id,
        requires_baseline_mapping,
        deposit_amount,
        deposit_payment_status,
        status
    )
    VALUES (
        v_order,
        'ORD-' || v_suffix,
        v_tenant,
        v_farm,
        v_request,
        v_service,
        true,
        10000,
        'PENDING'::system.survey_payment_status,
        'BASELINE_IN_PROGRESS'::system.survey_order_status
    );

    -- 12. Tạo drone mission
    INSERT INTO mission.drone_missions (
        id,
        tenant_id,
        farm_id,
        survey_order_id,
        mission_purpose,
        preparation_operation_id,
        drone_id,
        pilot_user_id,
        mission_code,
        status,
        processing_status,
        scheduled_at,
        scheduled_end_at,
        started_at,
        ended_at,
        flight_parameters,
        created_by
    )
    VALUES (
        v_mission,
        v_tenant,
        v_farm,
        v_order,
        'BASELINE_MAPPING'::system.mission_purpose,
        gen_random_uuid(),
        v_drone,
        v_manager,
        'MIS-' || v_suffix,
        'FLIGHT_COMPLETED'::system.mission_status,
        'NOT_UPLOADED'::system.processing_status,

        -- Scheduled start
        v_started_at - interval '15 minutes',

        -- Scheduled end
        v_ended_at + interval '15 minutes',

        -- Actual start: 01/10/2026
        v_started_at,

        -- Actual end: 30/11/2026
        v_ended_at,

        '{}'::jsonb,
        v_manager
    );

    -- 13. In thông tin dữ liệu test
    RAISE NOTICE 'Manager email: %',
        'uc04-manager-' || lower(v_suffix) || '@example.test';

    RAISE NOTICE 'Admin email: %',
        'uc04-admin-' || lower(v_suffix) || '@example.test';

    RAISE NOTICE 'Tenant ID: %', v_tenant;
    RAISE NOTICE 'Farm ID: %', v_farm;
    RAISE NOTICE 'SurveyOrder ID: %', v_order;
    RAISE NOTICE 'Mission ID: %', v_mission;

    RAISE NOTICE 'Flight window: % -> %',
        v_started_at,
        v_ended_at;

END $$;

