using ElevatorSystem.Application.Models;

namespace ElevatorSystem.Application.Interfaces;

public interface IScenarioReader
{
    Task<IEnumerable<ElevatorScenario>> ReadScenariosAsync(string filePath);
}