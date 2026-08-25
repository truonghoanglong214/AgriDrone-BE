namespace AgriDrone.Modules.Missions.Domain.Drones;

public interface IDroneStatusChangeRepository
{
    void Add(DroneStatusChange statusChange);
}