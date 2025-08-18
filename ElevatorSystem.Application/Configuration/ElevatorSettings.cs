namespace ElevatorSystem.Application.Configuration;

public class ElevatorSettings
{
    public const string SectionName = "ElevatorSettings";
    
    public int MinFloor { get; set; } = 1;
    public int MaxFloor { get; set; } = 10;
    public int NumberOfElevators { get; set; } = 1;
    public int FloorMovementTimeMs { get; set; } = 1000;
    public int LoadingTimeMs { get; set; } = 1000;
}