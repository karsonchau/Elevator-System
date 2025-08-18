using System.Text.Json;
using ElevatorSystem.Application.Interfaces;
using ElevatorSystem.Application.Models;
using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Application.Services;

public class ScenarioFileReader : IScenarioReader
{
    private readonly ILogger<ScenarioFileReader> _logger;

    public ScenarioFileReader(ILogger<ScenarioFileReader> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<ElevatorScenario>> ReadScenariosAsync(string filePath)
    {
        try
        {
            // Handle relative paths by resolving from the solution root
            var resolvedPath = ResolveFilePath(filePath);
            
            if (!File.Exists(resolvedPath))
            {
                _logger.LogError("Scenario file not found at path: {FilePath} (resolved to: {ResolvedPath})", filePath, resolvedPath);
                throw new FileNotFoundException($"Scenario file not found: {filePath} (resolved to: {resolvedPath})");
            }

            var fileContent = await File.ReadAllTextAsync(resolvedPath);
            
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                _logger.LogWarning("Scenario file is empty: {FilePath}", filePath);
                return Enumerable.Empty<ElevatorScenario>();
            }

            var scenarios = JsonSerializer.Deserialize<ElevatorScenario[]>(fileContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (scenarios == null || scenarios.Length == 0)
            {
                _logger.LogWarning("No valid scenarios found in file: {FilePath}", filePath);
                return Enumerable.Empty<ElevatorScenario>();
            }

            _logger.LogInformation("Successfully loaded {Count} scenarios from {FilePath} (resolved to: {ResolvedPath})", scenarios.Length, filePath, resolvedPath);
            return scenarios;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON from scenario file: {FilePath}", filePath);
            throw new InvalidOperationException($"Invalid JSON format in scenario file: {filePath}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read scenarios from file: {FilePath}", filePath);
            throw;
        }
    }

    private static string ResolveFilePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            return filePath;
        }

        // Find the solution root by looking for .sln file
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);

        while (directory != null && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            // If no .sln found, use current directory
            return Path.Combine(currentDirectory, filePath);
        }

        return Path.Combine(directory.FullName, filePath);
    }
}