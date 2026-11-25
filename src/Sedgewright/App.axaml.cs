using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Sedgewright.Infras.Quartz;
using Sedgewright.Infras.Storage;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Apps.UseCases;
using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.ViewModels.Agents;
using Sedgewright.ViewModels.Chat;
using Sedgewright.ViewModels.FirstRun;
using Sedgewright.ViewModels.MainWindow;
using Sedgewright.ViewModels.Mcp;
using Sedgewright.ViewModels.Orchestrations;
using Sedgewright.ViewModels.ScheduledJobs;
using Sedgewright.ViewModels.Settings;
using Sedgewright.ViewModels.Tools;
using Sedgewright.ViewModels.Workspace;
using Sedgewright.Infras.Services;
using Sedgewright.Views.FirstRun;
using Sedgewright.Views.MainWindow;
using Sedgewright.Views.ScheduledJobs;

namespace Sedgewright;

public partial class App : Avalonia.Application
{
    private ServiceProvider? _services;
    private IScheduler? _scheduler;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        _services = BuildServices();
        ILogger<App> logger = _services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Sedgewright desktop application starting.");
        await _services.GetRequiredService<SedgewrightDbContext>().Database.MigrateAsync();
        await _services.GetRequiredService<IEnvironmentSetup>().InitializeAsync();
        await StartSchedulerAsync(_services, logger);
        ISharedState sharedState = _services.GetRequiredService<ISharedState>();
        await sharedState.RefreshApplicationSettingsAsync();
        RequestedThemeVariant = sharedState.ApplicationSettings?.ThemeMode is ThemeMode.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += OnDesktopExit;
            ILLMProviderRepository providerRepository = _services.GetRequiredService<ILLMProviderRepository>();
            List<LLMProvider> configuredProviders = await providerRepository.GetAll();

            if (configuredProviders.Count > 0)
            {
                await ShowMainWindowAsync(desktop);
            }
            else
            {
                FirstRunWindow firstRunWindow = CreateFirstRunWindow();
                firstRunWindow.Completed += async (_, _) =>
                {
                    await ShowMainWindowAsync(desktop);
                    firstRunWindow.Close();
                };

                desktop.MainWindow = firstRunWindow;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        ServiceCollection services = new();
        SedgewrightTelemetry.Configure(services);
        services.AddSedgewrightStorage();
        services.AddSedgewrightScheduling();
        services.AddSingleton(new HttpClient());
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<FirstRunWindowViewModel>();
        services.AddSingleton<IEnvironmentSetup, EnvironmentSetup>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<IChatSessionService, ChatSessionService>();
        services.AddSingleton<IChatTargetCatalogService, ChatTargetCatalogService>();
        services.AddSingleton<IChatTargetDialogService, ChatTargetDialogService>();
        services.AddSingleton<IAgentSkillDiscoveryService, AgentSkillDiscoveryService>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AgentDefinitionsViewModel>();
        services.AddSingleton<IOrchestrationDialogService, OrchestrationDialogService>();
        services.AddSingleton<IMcpDefinitionDialogService, McpDefinitionDialogService>();
        services.AddSingleton<McpDefinitionsViewModel>();
        services.AddSingleton<IToolDefinitionDialogService, ToolDefinitionDialogService>();
        services.AddSingleton<ToolDefinitionsViewModel>();
        services.AddSingleton<OrchestrationDefinitionsViewModel>();
        services.AddSingleton<ScheduledJobsViewModel>();
        services.AddSingleton<IScheduledJobDialogService, ScheduledJobDialogService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<WorkspaceTreePanelViewModel>();
        services.AddSingleton<OrchestrationUseCases>();
        services.AddSingleton<ILLMProviderRepository, LLMProviderRepository>();
        services.AddSingleton<IWorkflowExecutionStrategy, SequentialExecutionStrategy>();
        services.AddSingleton<IWorkflowExecutionStrategy, ConcurrentExecutionStrategy>();
        services.AddSingleton<IWorkflowExecutionStrategy, GroupChatExecutionStrategy>();
        services.AddSingleton<IWorkflowExecutionStrategy, MagenticExecutionStrategy>();
        services.AddSingleton<IWorkflowExecutionStrategy, HandoffExecutionStrategy>();
        services.AddSingleton<IWorkflowExecutionStrategyFactory, WorkflowExecutionStrategyFactory>();
        return services.BuildServiceProvider();
    }

    private async Task ShowMainWindowAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        MainWindowViewModel viewModel = _services!.GetRequiredService<MainWindowViewModel>();
        await viewModel.Chat.InitializeAsync();
        viewModel.WorkspaceTree.Initialize();
        Window mainWindow = new MainWindow
        {
            DataContext = viewModel,
        };
        desktop.MainWindow = mainWindow;
        mainWindow.Show();

        SettingsViewModel settings = _services!.GetRequiredService<SettingsViewModel>();
        _ = settings.CheckForUpdatesSilentlyAsync();
    }

    private FirstRunWindow CreateFirstRunWindow()
    {
        ServiceProvider services = _services
            ?? throw new InvalidOperationException("Application services have not been initialized.");

        return new FirstRunWindow
        {
            DataContext = new FirstRunWindowViewModel(
                services.GetRequiredService<LLMProviderRepository>(),
                services.GetRequiredService<ISharedState>()),
        };
    }

    private async Task StartSchedulerAsync(ServiceProvider services, ILogger<App> logger)
    {
        try
        {
            IScheduler scheduler = await services.GetRequiredService<ISchedulerFactory>().GetScheduler();
            await scheduler.Start();
            _scheduler = scheduler;

            await using AsyncServiceScope scope = services.CreateAsyncScope();
            UnitResult<IApplicationError> reconcileResult = await scope.ServiceProvider
                .GetRequiredService<ScheduledJobUseCases>()
                .ReconcileAsync();
            if (reconcileResult.IsFailure)
            {
                logger.LogWarning("Scheduled jobs were only partially restored: {Errors}", reconcileResult.Error.Messages);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The job scheduler could not be started; scheduled jobs will not run.");
        }
    }

    private async void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_services is null)
        {
            return;
        }

        if (_scheduler is not null)
        {
            try
            {
                // Cancel running jobs so they record a Cancelled outcome instead of holding up shutdown.
                await _services.GetRequiredService<IScheduledJobScheduler>().InterruptAllAsync();
                await _scheduler.Shutdown(waitForJobsToComplete: true);
            }
            catch (Exception exception)
            {
                _services.GetRequiredService<ILogger<App>>().LogError(exception, "The job scheduler did not shut down cleanly.");
            }

            _scheduler = null;
        }

        await _services.DisposeAsync();
        _services = null;
    }
}
