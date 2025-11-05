using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Autofac.Extension;

/// <summary>
/// Designed for Autofac and other high-level features.
/// </summary>
public abstract class ExtendedHost(ILifetimeScope container) : IHost, IHostApplicationLifetime
{
    private readonly CancellationTokenSource _startedCtx = new();
    private readonly CancellationTokenSource _stoppingCts = new();
    private readonly CancellationTokenSource _stoppedCts = new();

    public CancellationToken ApplicationStarted => _startedCtx.Token;
    public CancellationToken ApplicationStopping => _stoppingCts.Token;
    public CancellationToken ApplicationStopped => _stoppedCts.Token;

    private bool _disposed = false;

    public required ILogger<ExtendedHost> Logger { protected get; init; }

    public ILifetimeScope Container => container;

    public IServiceProvider Services { get; } = new AutofacServiceProvider(container);

    private async Task CallLifetimeService<T>(Func<T, Task> func, string lifeTime) where T : class
    {
        Logger.LogTrace("trigger {interface}.{lifetime}",
            typeof(T).FullName,
            lifeTime);
        var services = container.Resolve<IEnumerable<T>>();
        foreach (var service in services)
        {
            await func.Invoke(service).ConfigureAwait(false);
        }
    }

    protected async Task StartServer<T>(IHttpApplication<T> application, CancellationToken abortStart) where T : notnull
    {
        await CallLifetimeService<IServer>(async (server) =>
        {
            await server.StartAsync(application, abortStart).ConfigureAwait(false);
        },
        nameof(IServer.StartAsync));
    }

    protected async Task StopServer(CancellationToken stopGracefullyShutdown)
    {
        await CallLifetimeService<IServer>(async (server) =>
        {
            await server.StopAsync(stopGracefullyShutdown).ConfigureAwait(false);
        },
        nameof(IServer.StopAsync));
    }

    private async Task BeforeStartServices(CancellationToken abortStart)
    {
        await CallLifetimeService<IHostedLifecycleService>(async (services) =>
        {
            await services.StartingAsync(abortStart).ConfigureAwait(false);
        },
        nameof(IHostedLifecycleService.StartingAsync));
    }

    private async Task StartServices(CancellationToken abortStart)
    {
        await CallLifetimeService<IHostedService>(async (services) =>
        {
            await services.StartAsync(abortStart).ConfigureAwait(false);
        },
        nameof(IHostedService.StartAsync));
    }

    private async Task AfterStartServices(CancellationToken abortStart)
    {
        await CallLifetimeService<IHostedLifecycleService>(async (services) =>
        {
            await services.StartedAsync(abortStart).ConfigureAwait(false);
        },
        nameof(IHostedLifecycleService.StartedAsync));
    }

    private async Task BeforeStopServices(CancellationToken stopGracefullyShutdown)
    {
        await CallLifetimeService<IHostedLifecycleService>(async (services) =>
        {
            await services.StoppingAsync(stopGracefullyShutdown).ConfigureAwait(false);
        },
        nameof(IHostedLifecycleService.StoppingAsync));
    }

    private async Task StopServices(CancellationToken stopGracefullyShutdown)
    {
        await CallLifetimeService<IHostedService>(async (services) =>
        {
            await services.StopAsync(stopGracefullyShutdown).ConfigureAwait(false);
        },
        nameof(IHostedService.StopAsync));
    }

    private async Task AfterStopServices(CancellationToken stopGracefullyShutdown)
    {
        await CallLifetimeService<IHostedLifecycleService>(async (services) =>
        {
            await services.StoppedAsync(stopGracefullyShutdown).ConfigureAwait(false);
        },
        nameof(IHostedLifecycleService.StoppedAsync));
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~ExtendedHost()
    {
        Dispose(disposing: false);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            CancellationTokenSource source = new();
            source.Cancel();
            WrappedStop(source.Token).Wait(CancellationToken.None);
            container.Dispose();
        }

        _disposed = true;
    }

    protected abstract Task Start(CancellationToken abortStart);

    protected abstract Task Main();

    protected abstract Task Stop(CancellationToken stopGracefullyShutdown);

    /// <summary>
    /// Start the application.
    /// </summary>
    public Task StartAsync(CancellationToken abortStart = new())
    {
        return Task.Run(async () =>
        {
            Logger.LogTrace("start application");
            try
            {
                await BeforeStartServices(abortStart).ConfigureAwait(false);
                await StartServices(abortStart).ConfigureAwait(false);
                await Start(abortStart).ConfigureAwait(false);
                await AfterStartServices(abortStart).ConfigureAwait(false);
                Logger.LogTrace("cancel CancellationToken:{interface}.{token}", nameof(IHostApplicationLifetime),
                                nameof(IHostApplicationLifetime.ApplicationStarted));
                await _startedCtx.CancelAsync().ConfigureAwait(false);
                abortStart.ThrowIfCancellationRequested();
                await Main().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogCritical("uncaught exception:{exception}", ex);
                throw;
            }
        }, CancellationToken.None);
    }

    public void StartInCurrentThread()
    {
        var none = CancellationToken.None;
        Logger.LogTrace("start application in current thread");
        try
        {
            BeforeStartServices(none).Wait(none);
            StartServices(none).Wait(none);
            Start(none).Wait(none);
            AfterStartServices(none).Wait(none);
            Logger.LogTrace("cancel CancellationToken:{interface}.{token}", nameof(IHostApplicationLifetime),
                            nameof(IHostApplicationLifetime.ApplicationStarted));
            _startedCtx.CancelAsync().Wait(none);
            Main().Wait(none);
        }
        catch (Exception ex)
        {
            Logger.LogCritical("uncaught exception:{exception}", ex);
            throw;
        }
    }

    private async Task WrappedStop(CancellationToken stopGracefullyShutdown)
    {
        Logger.LogTrace("stop application");
        try
        {
            Logger.LogTrace("cancel CancellationToken:{interface}.{token}", nameof(IHostApplicationLifetime), nameof(IHostApplicationLifetime.ApplicationStopping));
            await _stoppingCts.CancelAsync().ConfigureAwait(false);
            await BeforeStopServices(stopGracefullyShutdown).ConfigureAwait(false);
            await StopServices(stopGracefullyShutdown).ConfigureAwait(false);
            await Stop(stopGracefullyShutdown).ConfigureAwait(false);
            await AfterStopServices(stopGracefullyShutdown).ConfigureAwait(false);
        }
        finally
        {
            Logger.LogTrace("cancel CancellationToken:{interface}.{token}", nameof(IHostApplicationLifetime), nameof(IHostApplicationLifetime.ApplicationStopped));
            await _stoppedCts.CancelAsync().ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken stopGracefullyShutdown)
    {
        await WrappedStop(stopGracefullyShutdown).ConfigureAwait(false);
    }

    public void StopApplication()
    {
        StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
