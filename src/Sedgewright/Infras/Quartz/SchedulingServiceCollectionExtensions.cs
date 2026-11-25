using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Apps.UseCases;
using Sedgewright.Infras.Jobs;

namespace Sedgewright.Infras.Quartz;

/// <summary>
/// Registers Quartz and the scheduled job services without the generic host.
/// </summary>
public static class SchedulingServiceCollectionExtensions
{
    /// <summary>
    /// The maximum number of scheduled jobs that may run at the same time.
    /// </summary>
    public const int MaxConcurrentJobs = 3;

    /// <summary>
    /// Adds an in-memory Quartz scheduler and the services that keep it in sync with the <c>ScheduledJobs</c> table.
    /// The caller starts the scheduler after the database is migrated and shuts it down before disposing the provider.
    /// </summary>
    public static IServiceCollection AddSedgewrightScheduling(this IServiceCollection services)
    {
        services.AddQuartz(quartz =>
        {
            // Schedules are rebuilt from the database on every start, so nothing needs to survive a restart.
            quartz.UseInMemoryStore();
            quartz.UseDefaultThreadPool(MaxConcurrentJobs);
        });

        // Jobs are scheduled at runtime rather than through AddJob, so register the job type explicitly
        // to have Quartz resolve it, with its scoped dependencies, from a new scope on every fire.
        services.TryAddScoped<ScheduledJobQuartzJob>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IScheduledJobScheduler, QuartzScheduledJobScheduler>();
        services.AddScoped<ScheduledJobTargetResolver>();
        services.AddScoped<IScheduledJobRunner, ScheduledJobRunner>();
        services.AddScoped<ScheduledJobUseCases>();
        return services;
    }
}
