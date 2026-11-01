CREATE EXTENSION IF NOT EXISTS pgcrypto;

DO $$
DECLARE
    v_now timestamptz := clock_timestamp();
    v_suffix text :=
        upper(left(replace(gen_random_uuid()::text, '-', ''), 10));

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

    v_written_consent_id uuid := gen_random_uuid();
    v_flight_permission_id uuid := gen_random_uuid();

    v_mission_id uuid := gen_random_uuid();
    v_preparation_operation_id uuid := gen_random_uuid();
    v_preflight_operation_id uuid := gen_random_uuid();

    v_manager_role_id uuid;
    v_service_id uuid;

    v_deposit_amount numeric(18,2) := 10000;
    v_owner_email text;
    v_manager_email text;
    v_owner_phone text;
    v_manager_phone text;

    /*
     * Boundary mock khớp các dòng BOUNDARY trong CSV được cung cấp.
     * Upload cùng file sẽ không cần tạo boundary version mới.
     */
    v_placeholder_boundary geometry :=
        ST_GeomFromText(
            'POLYGON((' ||
            '106.4941327 10.4618749,' ||
            '106.4940086 10.4615431,' ||
            '106.4939875 10.4615490,' ||
            '106.4939962 10.4615648,' ||
            '106.4939597 10.4615784,' ||
            '106.4939362 10.4615889,' ||
            '106.4939463 10.4616206,' ||
            '106.4940576 10.4619000,' ||
            '106.4941327 10.4618749' ||
            '))',
            4326
        );
BEGIN
    v_owner_email :=
        'owner-' || lower(v_suffix) || '@example.test';

    v_manager_email :=
        'manager-' || lower(v_suffix) || '@example.test';

    v_owner_phone :=
        '09' || lpad(
            floor(random() * 100000000)::bigint::text,
            8,
            '0'
        );

    v_manager_phone :=
        '08' || lpad(
            floor(random() * 100000000)::bigint::text,
            8,
            '0'
        );

    --------------------------------------------------------------------------
    -- SYSTEM MANAGER ROLE
    --------------------------------------------------------------------------

    INSERT INTO identity.roles (
        code,
        name
    )
    VALUES (
        'SYSTEM_MANAGER',
        'System Manager'
    )
    ON CONFLICT (code) DO NOTHING;

    SELECT id
    INTO STRICT v_manager_role_id
    FROM identity.roles
    WHERE code = 'SYSTEM_MANAGER';

    --------------------------------------------------------------------------
    -- SURVEY SERVICE
    --------------------------------------------------------------------------

    SELECT id
    INTO STRICT v_service_id
    FROM survey.survey_services
    WHERE code = 'PLANT_HEALTH';

    --------------------------------------------------------------------------
    -- TENANT
    --------------------------------------------------------------------------

    INSERT INTO identity.tenants (
        id,
        code,
        name,
        status,
        created_at,
        updated_at
    )
    VALUES (
        v_tenant_id,
        'BASE-' || v_suffix,
        'Baseline Tenant ' || v_suffix,
        'ACTIVE'::system.general_status,
        v_now,
        v_now
    );

    --------------------------------------------------------------------------
    -- TENANT OWNER VÀ SYSTEM MANAGER
    -- PASSWORD: Test@123456
    --------------------------------------------------------------------------

    INSERT INTO identity.users (
        id,
        email,
        password_hash,
        full_name,
        phone,
        status,
        created_at,
        updated_at
    )
    VALUES
    (
        v_owner_id,
        v_owner_email,
        crypt('Test@123456', gen_salt('bf', 12)),
        'Baseline Tenant Owner',
        v_owner_phone,
        'ACTIVE'::system.user_status,
        v_now,
        v_now
    ),
    (
        v_manager_id,
        v_manager_email,
        crypt('Test@123456', gen_salt('bf', 12)),
        'Baseline System Manager',
        v_manager_phone,
        'ACTIVE'::system.user_status,
        v_now,
        v_now
    );

    --------------------------------------------------------------------------
    -- TENANT OWNER MEMBERSHIP
    --------------------------------------------------------------------------

    INSERT INTO identity.tenant_memberships (
        id,
        tenant_id,
        user_id,
        role,
        status,
        joined_at,
        created_at
    )
    VALUES (
        gen_random_uuid(),
        v_tenant_id,
        v_owner_id,
        'OWNER'::system.tenant_member_role,
        'ACTIVE'::system.general_status,
        v_now,
        v_now
    );

    --------------------------------------------------------------------------
    -- SYSTEM MANAGER ROLE
    --------------------------------------------------------------------------

    INSERT INTO identity.user_roles (
        user_id,
        role_id,
        assigned_at
    )
    VALUES (
        v_manager_id,
        v_manager_role_id,
        v_now
    );

    --------------------------------------------------------------------------
    -- SYSTEM MANAGER PROFILE
    --------------------------------------------------------------------------

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
        v_manager_profile_id,
        v_manager_id,
        'ACTIVE'::system.system_manager_profile_status,
        'AVAILABLE'::system.system_manager_availability_status,
        'QUALIFIED'::system.flight_qualification_status,
        v_now + interval '1 year',
        v_now,
        v_now,
        1
    );

    --------------------------------------------------------------------------
    -- FARM
    --------------------------------------------------------------------------

    INSERT INTO farm.farms (
        id,
        tenant_id,
        code,
        name,
        address,
        boundary,
        center_point,
        area_hectares,
        status,
        created_by,
        created_at,
        updated_at,
        version
    )
    VALUES (
        v_farm_id,
        v_tenant_id,
        'FARM-' || v_suffix,
        'Baseline Dragon Fruit Farm',
        'Long An, Vietnam',
        v_placeholder_boundary,
        ST_SetSRID(
            ST_MakePoint(106.49405, 10.46165),
            4326
        ),
        1.0000,
        'ACTIVE'::system.general_status,
        v_owner_id,
        v_now,
        v_now,
        1
    );

    --------------------------------------------------------------------------
    -- ASSIGN SYSTEM MANAGER
    --------------------------------------------------------------------------

    INSERT INTO identity.farm_manager_assignments (
        id,
        tenant_id,
        farm_id,
        system_manager_profile_id,
        assigned_by,
        assignment_reason,
        assigned_at,
        version
    )
    VALUES (
        v_assignment_id,
        v_tenant_id,
        v_farm_id,
        v_manager_profile_id,
        v_owner_id,
        'System Manager for baseline CSV testing',
        v_now - interval '2 days',
        1
    );

    --------------------------------------------------------------------------
    -- DRONE
    --------------------------------------------------------------------------

    INSERT INTO mission.drones (
        id,
        code,
        name,
        model,
        manufacturer,
        specifications,
        serial_number,
        registration_number,
        registration_date,
        registration_expiry_date,
        weight_kg,
        status,
        last_maintenance_at,
        next_maintenance_at,
        notes,
        created_at,
        updated_at
    )
    VALUES (
        v_drone_id,
        'DR-' || v_suffix,
        'Baseline Mapping Drone',
        'Matrice 350 RTK',
        'DJI',
        jsonb_build_object(
            'capabilities',
            jsonb_build_array(
                'BASELINE_MAPPING',
                'PLANT_HEALTH'
            )
        ),
        'SN-' || v_suffix,
        'REG-' || v_suffix,
        current_date - 30,
        current_date + 335,
        6.500,
        'AVAILABLE'::system.drone_status,
        v_now - interval '7 days',
        v_now + interval '6 months',
        'Flight completed; ready for CSV upload test',
        v_now - interval '30 days',
        v_now
    );

    --------------------------------------------------------------------------
    -- SURVEY REQUEST
    --------------------------------------------------------------------------

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
        estimated_pole_count,
        preferred_start_at,
        preferred_end_at,
        notes,
        status,
        created_at,
        updated_at
    )
    VALUES (
        v_request_id,
        'REQ-' || v_suffix,
        'EXISTING_FARM_SURVEY'::system.survey_request_kind,
        v_tenant_id,
        v_farm_id,
        v_owner_id,
        v_service_id,
        'TENANT_OWNER',
        'BASELINE-SEED-' || v_suffix,
        'Baseline Tenant Owner',
        v_owner_email,
        v_owner_phone,
        'Baseline Dragon Fruit Farm',
        'Long An, Vietnam',
        1.0000,
        ST_SetSRID(
            ST_MakePoint(106.49405, 10.46165),
            4326
        ),
        61,
        v_now - interval '4 hours',
        v_now - interval '1 hour',
        'Baseline mission completed before plant GPS CSV upload',
        'APPROVED'::system.survey_request_status,
        v_now - interval '3 days',
        v_now - interval '2 days'
    );

    --------------------------------------------------------------------------
    -- PLACEHOLDER APPROVED FARM BOUNDARY
    --------------------------------------------------------------------------

    INSERT INTO farm.farm_boundaries (
        id,
        tenant_id,
        farm_id,
        version_number,
        geometry,
        source,
        source_survey_request_id,
        submitted_by,
        evidence_reference,
        status,
        reviewed_by,
        reviewed_at,
        review_reason,
        created_at,
        updated_at
    )
    VALUES (
        v_boundary_id,
        v_tenant_id,
        v_farm_id,
        1,
        v_placeholder_boundary,
        'SYSTEM_MANAGER'::system.farm_boundary_source,
        v_request_id,
        v_manager_id,
        'seed://placeholder-before-csv-upload',
        'APPROVED'::system.farm_boundary_status,
        v_manager_id,
        v_now - interval '2 days',
        'Boundary seeded to match the supplied baseline CSV',
        v_now - interval '2 days',
        v_now - interval '2 days'
    );

    --------------------------------------------------------------------------
    -- SURVEY ORDER: BASELINE ĐANG THỰC HIỆN
    -- CHƯA CÓ FARM BASE MAP
    --------------------------------------------------------------------------

    INSERT INTO survey.survey_orders (
        id,
        order_number,
        tenant_id,
        farm_id,
        survey_request_id,
        survey_service_id,
        farm_boundary_version_id,
        scope_confirmed_by,
        scope_confirmed_at,
        requires_baseline_mapping,
        deposit_amount,
        deposit_payment_status,
        deposit_confirmed_at,
        scope_type,
        status,
        created_at,
        updated_at
    )
    VALUES (
        v_order_id,
        'ORD-' || v_suffix,
        v_tenant_id,
        v_farm_id,
        v_request_id,
        v_service_id,
        v_boundary_id,
        v_owner_id,
        v_now - interval '2 days',
        true,
        v_deposit_amount,
        'PAID'::system.survey_payment_status,
        v_now - interval '2 days',
        'FARM_WIDE'::system.survey_scope_type,
        'BASELINE_IN_PROGRESS'::system.survey_order_status,
        v_now - interval '3 days',
        v_now
    );

    --------------------------------------------------------------------------
    -- BASELINE DEPOSIT PAYMENT
    --------------------------------------------------------------------------

    INSERT INTO survey.survey_payments (
        id,
        survey_order_id,
        amount,
        currency,
        provider,
        provider_reference,
        status,
        purpose,
        confirmed_at,
        created_at,
        updated_at
    )
    VALUES (
        v_payment_id,
        v_order_id,
        v_deposit_amount,
        'VND',
        'LOCAL_TEST',
        'BASELINE-PAY-' || v_suffix,
        'PAID'::system.survey_payment_status,
        'BaselineDeposit',
        v_now - interval '2 days',
        v_now - interval '2 days',
        v_now - interval '2 days'
    );

    UPDATE survey.survey_orders
    SET
        deposit_payment_id = v_payment_id,
        updated_at = v_now
    WHERE id = v_order_id;

    --------------------------------------------------------------------------
    -- CONFIRMED BASELINE APPOINTMENT
    --------------------------------------------------------------------------

    INSERT INTO survey.survey_appointments (
        id,
        survey_order_id,
        purpose,
        revision_number,
        proposed_by_system_manager_id,
        proposed_start_at,
        proposed_end_at,
        status,
        confirmed_by_tenant_owner_id,
        confirmed_at,
        created_at,
        updated_at
    )
    VALUES (
        v_appointment_id,
        v_order_id,
        'BASELINE_MAPPING'::system.survey_appointment_purpose,
        1,
        v_manager_id,
        v_now - interval '3 hours',
        v_now - interval '1 hour',
        'CONFIRMED'::system.survey_appointment_status,
        v_owner_id,
        v_now - interval '1 day',
        v_now - interval '1 day',
        v_now - interval '1 day'
    );

    --------------------------------------------------------------------------
    -- CAPTURE AUTHORIZATIONS
    --------------------------------------------------------------------------

    INSERT INTO survey.survey_capture_authorizations (
        id,
        survey_order_id,
        authorization_type,
        status,
        evidence_reference,
        issued_at,
        verified_at,
        expires_at,
        verified_by,
        created_at,
        updated_at
    )
    VALUES
    (
        v_written_consent_id,
        v_order_id,
        'WRITTEN_DATA_USE_CONSENT'
            ::system.survey_capture_authorization_type,
        'VERIFIED'
            ::system.survey_capture_authorization_status,
        'seed://written-data-use-consent',
        v_now - interval '2 days',
        v_now - interval '2 days',
        v_now + interval '1 year',
        v_manager_id,
        v_now - interval '2 days',
        v_now - interval '2 days'
    ),
    (
        v_flight_permission_id,
        v_order_id,
        'FLIGHT_PERMISSION'
            ::system.survey_capture_authorization_type,
        'VERIFIED'
            ::system.survey_capture_authorization_status,
        'seed://flight-permission',
        v_now - interval '2 days',
        v_now - interval '2 days',
        v_now + interval '1 year',
        v_manager_id,
        v_now - interval '2 days',
        v_now - interval '2 days'
    );

    --------------------------------------------------------------------------
    -- MOCK BASELINE MISSION SAU FINALIZE
    -- SẴN SÀNG CHO BƯỚC UPLOAD CSV
    --------------------------------------------------------------------------

    INSERT INTO mission.drone_missions (
        id,
        tenant_id,
        farm_id,
        zone_id,
        drone_id,
        pilot_user_id,
        mission_code,
        mission_type,
        mission_purpose,
        survey_order_id,
        preparation_operation_id,
        status,
        processing_status,
        scheduled_at,
        scheduled_end_at,
        started_at,
        ended_at,
        flight_route,
        detected_plant_count,
        flight_parameters,
        preflight_confirmed_at,
        preflight_confirmed_by,
        preflight_checklist_answers,
        preflight_checklist_version,
        preflight_notes,
        preflight_operation_id,
        preflight_suitable_for_flight,
        farm_boundary_version_id,
        source_map_version_id,
        requires_baseline_completion,
        scope_zone_ids,
        scope_type,
        scope_fingerprint,
        scope_snapshot,
        notes,
        created_by,
        created_at,
        updated_at
    )
    VALUES (
        v_mission_id,
        v_tenant_id,
        v_farm_id,
        NULL,
        v_drone_id,
        v_manager_id,
        'MIS-BASE-' || v_suffix,
        'MAPPING'::system.mission_type,
        'BASELINE_MAPPING'::system.mission_purpose,
        v_order_id,
        v_preparation_operation_id,
        'READY_FOR_PROCESSING'::system.mission_status,
        'UPLOADED'::system.processing_status,
        v_now - interval '3 hours',
        v_now - interval '1 hour',
        v_now - interval '2 hours 50 minutes',
        v_now - interval '1 hour 10 minutes',
        ST_GeomFromText(
            'LINESTRING(' ||
            '106.49395 10.46150,' ||
            '106.49405 10.46170,' ||
            '106.49415 10.46185' ||
            ')',
            4326
        ),
        NULL,
        jsonb_build_object(
            'altitudeMeters', 30,
            'speedMetersPerSecond', 5,
            'overlapPercent', 75
        ),
        v_now - interval '3 hours 10 minutes',
        v_manager_id,
        jsonb_build_object(
            'weatherSafe', true,
            'droneReady', true,
            'flightPermissionVerified', true
        ),
        'seed-v1',
        'Preflight completed by seed script',
        v_preflight_operation_id,
        true,
        v_boundary_id,
        NULL,
        false,
        ARRAY[]::uuid[],
        'FARM_WIDE',
        'FARM_WIDE',
        '[]'::jsonb,
        'Mock finalized; waiting for plant GPS CSV upload',
        v_manager_id,
        v_now - interval '1 day',
        v_now
    );

    --------------------------------------------------------------------------
    -- ASSERT: DỪNG TRƯỚC UPLOAD CSV
    --------------------------------------------------------------------------

    IF EXISTS (
        SELECT 1
        FROM mission.mission_plant_gps
        WHERE mission_id = v_mission_id
    ) THEN
        RAISE EXCEPTION
            'Mission vừa tạo không được có plant GPS trước CSV upload';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM farm.mapping_candidate_batches
        WHERE mission_id = v_mission_id
    ) THEN
        RAISE EXCEPTION
            'Mission vừa tạo không được có mapping candidate batch';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM farm.farm_base_map_versions
        WHERE farm_id = v_farm_id
    ) THEN
        RAISE EXCEPTION
            'Farm vừa tạo không được có base map trước CSV publish';
    END IF;

    --------------------------------------------------------------------------
    -- RESULT
    --------------------------------------------------------------------------

    RAISE NOTICE '============================================================';
    RAISE NOTICE 'Tenant ID            : %', v_tenant_id;
    RAISE NOTICE 'Tenant Owner ID      : %', v_owner_id;
    RAISE NOTICE 'Owner email          : %', v_owner_email;
    RAISE NOTICE 'Owner phone          : %', v_owner_phone;
    RAISE NOTICE 'Owner password       : Test@123456';
    RAISE NOTICE 'Manager ID           : %', v_manager_id;
    RAISE NOTICE 'Manager email        : %', v_manager_email;
    RAISE NOTICE 'Manager phone        : %', v_manager_phone;
    RAISE NOTICE 'Manager password     : Test@123456';
    RAISE NOTICE 'Manager Profile ID   : %', v_manager_profile_id;
    RAISE NOTICE 'Farm ID              : %', v_farm_id;
    RAISE NOTICE 'CSV-matched Boundary : %', v_boundary_id;
    RAISE NOTICE 'Drone ID             : %', v_drone_id;
    RAISE NOTICE 'Survey Request ID    : %', v_request_id;
    RAISE NOTICE 'Survey Order ID      : %', v_order_id;
    RAISE NOTICE 'Appointment ID       : %', v_appointment_id;
    RAISE NOTICE 'Deposit Payment ID   : %', v_payment_id;
    RAISE NOTICE 'Mission ID           : %', v_mission_id;
    RAISE NOTICE 'Order status         : BASELINE_IN_PROGRESS';
    RAISE NOTICE 'Mission purpose      : BASELINE_MAPPING';
    RAISE NOTICE 'Mission status       : READY_FOR_PROCESSING';
    RAISE NOTICE 'Processing status    : UPLOADED';
    RAISE NOTICE 'Finalize API         : MOCKED IN DATABASE ONLY';
    RAISE NOTICE 'Plant GPS rows       : 0';
    RAISE NOTICE 'Mapping batch        : NONE';
    RAISE NOTICE 'Farm base map        : NONE';
    RAISE NOTICE '============================================================';
    RAISE NOTICE 'NEXT API:';
    RAISE NOTICE
        'POST /api/system-manager/farms/%/missions/%/plant-gps/csv',
        v_farm_id,
        v_mission_id;
    RAISE NOTICE '============================================================';
END $$;




