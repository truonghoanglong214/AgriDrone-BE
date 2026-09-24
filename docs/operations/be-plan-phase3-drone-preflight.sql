-- Be-Plan Phase 3: read-only preflight for system-owned Drone conversion.
-- Run against the target database before applying ConvertDronesToSystemOwned.
-- Every collision row must be resolved by an operator; never merge physical
-- drones automatically.

select
    count(*) as drone_count,
    count(distinct tenant_id) as legacy_tenant_count,
    count(*) filter (where deleted_at is not null) as archived_drone_count
from mission.drones;

select
    'code' as key_type,
    code as key_value,
    count(*) as collision_count,
    array_agg(id order by id) as drone_ids,
    array_agg(tenant_id order by tenant_id) as tenant_ids
from mission.drones
group by code
having count(*) > 1

union all

select
    'serial_number',
    serial_number,
    count(*),
    array_agg(id order by id),
    array_agg(tenant_id order by tenant_id)
from mission.drones
where serial_number is not null
group by serial_number
having count(*) > 1

union all

select
    'registration_number',
    registration_number,
    count(*),
    array_agg(id order by id),
    array_agg(tenant_id order by tenant_id)
from mission.drones
where registration_number is not null
group by registration_number
having count(*) > 1
order by key_type, key_value;

-- Must return zero rows. These rows would already violate the legacy
-- composite FK semantics or indicate damaged historical data.
select
    mission.id as mission_id,
    mission.tenant_id as mission_tenant_id,
    mission.drone_id,
    drone.tenant_id as drone_tenant_id
from mission.drone_missions mission
left join mission.drones drone on drone.id = mission.drone_id
where drone.id is null
   or drone.tenant_id <> mission.tenant_id
order by mission.id;

-- Existing conflicting schedules for the same physical Drone must return
-- zero rows before the global exclusion constraint is installed.
select
    first_mission.drone_id,
    first_mission.id as first_mission_id,
    first_mission.tenant_id as first_tenant_id,
    second_mission.id as second_mission_id,
    second_mission.tenant_id as second_tenant_id,
    tstzrange(
        first_mission.scheduled_at,
        first_mission.scheduled_end_at,
        '[)') as first_window,
    tstzrange(
        second_mission.scheduled_at,
        second_mission.scheduled_end_at,
        '[)') as second_window
from mission.drone_missions first_mission
join mission.drone_missions second_mission
  on second_mission.drone_id = first_mission.drone_id
 and second_mission.id > first_mission.id
where first_mission.status in (
        'SCHEDULED'::system.mission_status,
        'IN_FLIGHT'::system.mission_status)
  and second_mission.status in (
        'SCHEDULED'::system.mission_status,
        'IN_FLIGHT'::system.mission_status)
  and first_mission.scheduled_at is not null
  and first_mission.scheduled_end_at is not null
  and second_mission.scheduled_at is not null
  and second_mission.scheduled_end_at is not null
  and tstzrange(
        first_mission.scheduled_at,
        first_mission.scheduled_end_at,
        '[)') && tstzrange(
        second_mission.scheduled_at,
        second_mission.scheduled_end_at,
        '[)')
order by first_mission.drone_id, first_mission.scheduled_at;
