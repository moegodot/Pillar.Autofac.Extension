using Autofac;
using Autofac.Diagnostics.DotGraph;
using Autofac.Extension;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;

namespace Sample;

public static class Program
{
    class TargetClass
    {
        public required string Name { get; init; }

        public required object Value { get; init; }

        public required IOptions<SimpleConfigure> Options { get; init; }

        public required IOptionsMonitor<ComplexConfig> ComplexOptions { get; init; }

        public required ILogger<TargetClass> Logger { get; init; }

        public void Print()
        {
            var current = ComplexOptions.Get("complex c");
            Logger.LogInformation($"Name: {Name}, Value: {Value}\nConfigure: {Options.Value.ToString()}");
            Logger.LogInformation(
                $"ComplexOptions:{current.IsCanceled} && " +
                $"{current.Configured} && " +
                $"{current.PostConfigured} && " +
                $"{current.Validated}");
        }
    }

    class SimpleConfigure
    {
        public bool DefaultName { get; set; } = false;
        public bool B { get; set; } = false;
        public bool All { get; set; } = false;

        public override string ToString()
        {
            return $"With DefaultName: {DefaultName}(should true), With B: {B}(should false), With All:{All}(should true)";
        }
    }

    class ComplexConfig
    {
        public bool? Configured { get; set; } = null;

        public bool? PostConfigured { get; set; } = null;

        public bool? Validated { get; set; } = null;

        public string IsCanceled { get; set; } = "unknown";
    }

    class TestProvider : IConfigurationProvider, IChangeToken
    {
        public const string Key = nameof(ComplexConfig.IsCanceled);

        public static CancellationTokenSource Source { get; } = new();

        private bool entered = false;

        public TestProvider()
        {
            Source.Token.Register(() => { if (HasChanged) { Load(); return; } });
        }

        public bool HasChanged
        {
            get
            {
                var reqwest = Source.IsCancellationRequested;

                if (!reqwest)
                {
                    return reqwest;
                }

                if (entered)
                {
                    return reqwest;
                }

                entered = true;
                try
                {
                    var keys = Actions.Keys.ToArray();
                    foreach (var key in keys)
                    {
                        if (Actions.TryGetValue(key, out var action))
                        {
                            action.Invoke();
                        }
                    }
                }
                finally
                {
                    entered = false;
                }

                return reqwest;
            }
        }

        public bool ActiveChangeCallbacks => true;

        private string _value = "unknown";

        private Dictionary<int, Action> Actions = [];

        private int _next = 0;

        public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
        {
            if (parentPath == null)
            {
                return [Key];
            }
            return [];
        }

        public IChangeToken GetReloadToken()
        {
            return this;
        }

        public void Load()
        {
            _value = Source.IsCancellationRequested ? "canceled" : "not canceled";
        }

        public void Set(string key, string? value)
        {

        }

        public bool TryGet(string key, out string? value)
        {
            value = null;

            if (key == Key)
            {
                value = _value;
                return true;
            }

            return false;
        }

        private class Dis(int i, Dictionary<int, Action> dict) : IDisposable
        {
            public void Dispose()
            {
                GC.SuppressFinalize(this);
                dict.Remove(i);
            }
        }

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
        {
            Actions.Add(_next, () => callback(state));
            _next++;
            return new Dis(_next - 1, Actions);
        }
    }
    class TestSource : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new TestProvider();
        }
    }

    class Host : ExtendedHost
    {
        public Host(ILifetimeScope container) : base(container)
        {
        }

        protected override Task Main()
        {
            Console.WriteLine("Run in thread: " + Thread.CurrentThread.Name);
            Container.Resolve<TargetClass>().Print();
            TestProvider.Source.Cancel();
            Container.Resolve<TargetClass>().Print();
            return Task.CompletedTask;
        }

        protected override Task Start(CancellationToken abortStart)
        {
            return Task.CompletedTask;
        }

        protected override Task Stop(CancellationToken stopGracefullyShutdown)
        {
            return Task.CompletedTask;
        }
    }

    static void HostTest()
    {
        var builder = new ExtendedHostBuilder(
            new ExtendedHostEnvironment("test", ".", "Development"));

        builder.ConfigureServices((_, collection) =>
        {
            collection.AddSingleton(ctx => "Hello,but wrong");
            collection.AddSingleton(ctx => "Hello,OK!");
            collection.AddSingleton<object>(1);
            collection.AddSingleton<object>(2);
        });

        builder.ConfigureContainer((_, containerBuilder) =>
        {
            containerBuilder.AddLogging(static (f) =>
            {
                f.AddConsole(_ =>
                {
                }).AddFilter(null, LogLevel.Trace);
            });
            containerBuilder.AddOptions();
            containerBuilder.Configure<SimpleConfigure>(Options.DefaultName, (c) =>
            {
                c.DefaultName = true;
            });
            containerBuilder.Configure<SimpleConfigure>(null, (c) =>
            {
                c.All = true;
            });
            containerBuilder.Configure<SimpleConfigure>("b", (c) =>
            {
                c.B = false;
            });
            containerBuilder.RegisterType<TargetClass>().AsSelf();
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Add(new TestSource());
        });

        builder.BindOptions<ComplexConfig>("complex c", (config) =>
        {
            return config;
        });

        builder.ConfigureContainer((_, containerBuilder) =>
        {
            containerBuilder.Configure<ComplexConfig>("complex c", (c) =>
            {
                c.Configured = false;
            });
            containerBuilder.Configure<ComplexConfig>("complex c", (c) =>
            {
                c.Configured = true;
            });
            containerBuilder.PostConfigure<ComplexConfig>("complex c", (c) =>
            {
                c.PostConfigured = false;
            });
            containerBuilder.PostConfigure<ComplexConfig>("complex c", (c) =>
            {
                c.PostConfigured = true;
            });
            containerBuilder.ValidateOptions<ComplexConfig>("complex c", (c) =>
            {
                c.Validated = true;
                return true;
            }, "you should not see this");
        });

        builder.ConfigureIContainer((_, container) =>
        {
            container.OutputDotGraph();
        });

        builder.RegisterHost<Host>();

        var host = (ExtendedHost)builder.Build();

        host.StartInCurrentThread();
        host.StopAsync(CancellationToken.None).Wait();
    }

    private static void Main(string[] args)
    {
        Thread.CurrentThread.Name = "Main Thread + Start Thread";
        Console.WriteLine("------- Host Test -------");
        HostTest();
    }

    public const string ExpectedOutput = @"
------- Host Test -------
Resolve Autofac.Extension.ExtendedHost[graph]
trce: Autofac.Extension.ExtendedHost[0]
      start application in current thread
trce: Autofac.Extension.ExtendedHost[0]
      trigger Microsoft.Extensions.Hosting.IHostedLifecycleService.StartingAsync
Resolve System.Collections.Generic.IEnumerable`1[[Microsoft.Extensions.Hosting.IHostedLifecycleService, Microsoft.Extensions.Hosting.Abstractions, Version=9.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60]][graph]
trce: Autofac.Extension.ExtendedHost[0]
      trigger Microsoft.Extensions.Hosting.IHostedService.StartAsync
Resolve System.Collections.Generic.IEnumerable`1[[Microsoft.Extensions.Hosting.IHostedService, Microsoft.Extensions.Hosting.Abstractions, Version=9.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60]][graph]
trce: Autofac.Extension.ExtendedHost[0]
      trigger Microsoft.Extensions.Hosting.IHostedLifecycleService.StartedAsync
Resolve System.Collections.Generic.IEnumerable`1[[Microsoft.Extensions.Hosting.IHostedLifecycleService, Microsoft.Extensions.Hosting.Abstractions, Version=9.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60]][graph]
trce: Autofac.Extension.ExtendedHost[0]
      cancel CancellationToken:IHostApplicationLifetime.ApplicationStarted
Run in thread: Main Thread + Start Thread
Resolve Sample.Program+TargetClass[graph]
info: Sample.Program.TargetClass[0]
      Name: Hello,OK!, Value: 2
Configure: With DefaultName: True(should true), With B: False(should false), With All:True(should true)
info: Sample.Program.TargetClass[0]
      ComplexOptions:not canceled && True && True && True
Resolve Sample.Program+TargetClass[graph]
info: Sample.Program.TargetClass[0]
      Name: Hello,OK!, Value: 2
Configure: With DefaultName: True(should true), With B: False(should false), With All:True(should true)
info: Sample.Program.TargetClass[0]
      ComplexOptions:canceled && True && True && True
trce: Autofac.Extension.ExtendedHost[0]
      stop application
trce: Autofac.Extension.ExtendedHost[0]
      cancel CancellationToken:IHostApplicationLifetime.ApplicationStopping
trce: Autofac.Extension.ExtendedHost[0]
      trigger Microsoft.Extensions.Hosting.IHostedLifecycleService.StoppingAsync
Resolve System.Collections.Generic.IEnumerable`1[[Microsoft.Extensions.Hosting.IHostedLifecycleService, Microsoft.Extensions.Hosting.Abstractions, Version=9.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60]][graph]
trce: Autofac.Extension.ExtendedHost[0]
      trigger Microsoft.Extensions.Hosting.IHostedService.StopAsync
Resolve System.Collections.Generic.IEnumerable`1[[Microsoft.Extensions.Hosting.IHostedService, Microsoft.Extensions.Hosting.Abstractions, Version=9.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60]][graph]
trce: Autofac.Extension.ExtendedHost[0]
      trigger Microsoft.Extensions.Hosting.IHostedLifecycleService.StoppedAsync
Resolve System.Collections.Generic.IEnumerable`1[[Microsoft.Extensions.Hosting.IHostedLifecycleService, Microsoft.Extensions.Hosting.Abstractions, Version=9.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60]][graph]
trce: Autofac.Extension.ExtendedHost[0]
      cancel CancellationToken:IHostApplicationLifetime.ApplicationStopped
";
}
