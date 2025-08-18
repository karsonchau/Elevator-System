namespace ElevatorSystem.Application.Models;

public class ElevatorScenario
{
    public int CurrentFloor { get; set; }
    public int DestinationFloor { get; set; }
    public int RequestTimeMs { get; set; }
}