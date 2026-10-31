DO $$
DECLARE
    v_mission uuid := gen_random_uuid();
    v_tenant uuid := 'a83f601b-3d5b-4e14-b735-28b46b213438';
    v_farm uuid := 'b9d9d8a3-94a5-4390-a007-9017a2a131fa';
    v_order uuid := 'ce395064-e1d5-4a50-8e76-8bc6a47dbae2';
    v_drone uuid := '18d6ea16-814f-4a15-b65b-dfd7fcc3bf7c';
    v_manager uuid := '37c05ed0-dc92-45b0-8bf5-39d57e7ffddf';
    v_started_at timestamptz := TIMESTAMPTZ '2026-10-01 00:00:00+00:00';
    v_ended_at timestamptz := TIMESTAMPTZ '2026-11-30 23:59:59+00:00';
BEGIN
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
        requires_baseline_completion,
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
        'PLANT_HEALTH'::system.mission_purpose,
        gen_random_uuid(),
        v_drone,
        v_manager,
        'MIS-DIS-E6A73A696F',
        'FLIGHT_COMPLETED'::system.mission_status,
        'NOT_UPLOADED'::system.processing_status,
        true,
        v_started_at - interval '15 minutes',
        v_ended_at + interval '15 minutes',
        v_started_at,
        v_ended_at,
        '{}'::jsonb,
        v_manager
    );

    RAISE NOTICE 'Mission ID: %', v_mission;
END $$;

