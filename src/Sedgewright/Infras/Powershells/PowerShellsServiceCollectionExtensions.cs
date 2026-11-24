using Microsoft.Extensions.DependencyInjection;

namespace Sedgewright.Infrastructure.Powershells;

/// <summary>
/// Service collection extension for registering PowerShell execution services.
/// </summary>
public static class PowerShellsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PowerShell execution service in the dependency injection container.
    /// </summary>
    public static IServiceCollection AddPowerShellExecution(this IServiceCollection services)
    {
        // Removed PowerShellExecutionService registration. No longer needed.
        return services;
    }
}
