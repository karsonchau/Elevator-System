namespace ElevatorSystem.Application.Configuration;

public class SimulationSettings
{
    public const string SectionName = "SimulationSettings";
    
    public string ScenarioFilePath { get; set; } = "scenarios.json";
}