using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Autofac.Extension;

/// <summary>
/// Designed for Autofac and other high-level features.
/// </summary>
public sealed class ExtendedHostBuilder : IHostBuilder
{
    public ExtendedHostBuilder(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        Dictionary<object, object> properties = [];
        Properties = properties;
        Environment = environment;
        Context = new HostBuilderContext(Properties)
        {
            Configuration = null!,
            HostingEnvironment = Environment,
        };
    }

    private bool _built = false;

    private HostBuilderContext Context { get; }

    private IHostEnvironment Environment { get; }

    public IDictionary<object, object> Properties { get; }

    private List<Action<HostBuilderContext, IServiceCollection>> ConfigureServicesDelegates { get; } = new();
    private List<Action<IConfigurationBuilder>> ConfigureHostConfigurationDelegates { get; } = new();
    private List<Action<HostBuilderContext, IConfigurationBuilder>> ConfigureAppConfigurationDelegates { get; } = new();
    private List<Action<HostBuilderContext, ContainerBuilder>> ConfigureContainerDelegates { get; } = new();
    private List<Action<HostBuilderContext, IContainer>> ConfigureIContainerDelegate { get; } = new();

    public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate)
    {
        ArgumentNullException.ThrowIfNull(configureDelegate);
        ConfigureHostConfigurationDelegates.Add(configureDelegate);
        return this;
    }

    public IHostBuilder ConfigureIContainer(Action<HostBuilderContext, IContainer> configureDelegate)
    {
        ArgumentNullException.ThrowIfNull(configureDelegate);
        ConfigureIContainerDelegate.Add(configureDelegate);
        return this;
    }

    public IHostBuilder ConfigureAppConfiguration(
        Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        ArgumentNullException.ThrowIfNull(configureDelegate);
        ConfigureAppConfigurationDelegates.Add(configureDelegate);
        return this;
    }

    public IHostBuilder BindOptions<TOptions>(string? name, Func<IConfiguration, IConfiguration> configuration)
        where TOptions : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ConfigureContainerDelegates.Add((_, builder) =>
        {
            builder.Register((componentContext) =>
            {
                var config = configuration.Invoke(componentContext.Resolve<IConfiguration>());

                return new ConfigurationChangeTokenSource<TOptions>(name, config);
            })
            .As<IOptionsChangeTokenSource<TOptions>>();

            builder.Configure<TOptions, IConfiguration>(name, (options, config) =>
            {
                config.Bind(options);
            });
        });
        return this;
    }

    public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
    {
        ArgumentNullException.ThrowIfNull(configureDelegate);
        ConfigureServicesDelegates.Add(configureDelegate);
        return this;
    }

    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
        IServiceProviderFactory<TContainerBuilder> factory) where TContainerBuilder : notnull
    {
        // should not call this
        // we use Autofac
        throw new NotSupportedException();
    }

    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
        Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory) where TContainerBuilder : notnull
    {
        // should not call this
        // we use Autofac
        throw new NotSupportedException();
    }

    public void RegisterHost<T>() where T : ExtendedHost
    {
        ConfigureContainerDelegates.Add((_, builder) =>
        {
            builder.RegisterType<T>().As<ExtendedHost>().As<IHost>().As<IHostApplicationLifetime>().SingleInstance();
        });
    }

    public IHostBuilder ConfigureContainer<TContainerBuilder>(
        Action<HostBuilderContext, TContainerBuilder> configureDelegate)
    {
        if (configureDelegate is Action<HostBuilderContext, ContainerBuilder> action)
        {
            ConfigureContainerDelegates.Add(action);
        }
        else
        {
            // should not call this
            // we use Autofac
            throw new NotSupportedException();
        }

        return this;
    }

    public IHostBuilder ConfigureContainer(Action<HostBuilderContext, ContainerBuilder> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ConfigureContainerDelegates.Add(action);
        return this;
    }

    /// <summary>
    /// The registered components in IServiceCollection will be overridden by the components registered in ContainerBuilder.
    /// </summary>
    /// <returns>ExtendedHost</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public IHost Build()
    {
        // ****************
        // If you want to modify this file,
        // https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting/src/HostBuilder.cs
        // mey be a good place
        // ****************
        if (_built)
        {
            throw new InvalidOperationException("The host has already been built.");
        }
        _built = true;
        IContainer container;
        // build system configuration first
        {
            ConfigurationBuilder builder = new();
            builder.AddInMemoryCollection();
            foreach (var action in ConfigureHostConfigurationDelegates)
            {
                action(builder);
            }
            Context.Configuration = builder.Build();
        }
        // build application configuration then
        {
            ConfigurationBuilder builder = new();
            builder.AddConfiguration(Context.Configuration, true);
            foreach (var action in ConfigureAppConfigurationDelegates)
            {
                action(Context, builder);
            }
            Context.Configuration = builder.Build();
        }
        // build services lastly
        {
            ServiceCollection serviceCollection = new();

            foreach (var action in ConfigureServicesDelegates)
            {
                action(Context, serviceCollection);
            }

            ContainerBuilder containerBuilder = new();
            containerBuilder.Populate(serviceCollection);

            foreach (var action in ConfigureContainerDelegates)
            {
                action(Context, containerBuilder);
            }

            // inject things we need
            containerBuilder.RegisterInstance(Environment).As<IHostEnvironment>().SingleInstance();
            containerBuilder.RegisterInstance(Context).As<HostBuilderContext>().SingleInstance();
            containerBuilder.RegisterInstance(Context.Configuration).As<IConfiguration>().SingleInstance();
            // IHostApplicationLifetime was registered by RegisterHost<T>() method

            container = containerBuilder.Build();
        }
        {
            foreach (var action in ConfigureIContainerDelegate)
            {
                action(Context, container);
            }
        }
        return container.Resolve<ExtendedHost>();
    }
}
