using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Autofac.Extension;
public static class ConfigurationExtension
{
    private class OptionsWrapper<T> : IOptions<T>
        where T : class, new()
    {
        public T Value { get; init; }

        public OptionsWrapper(IOptionsFactory<T> newOptions)
        {
            Value = newOptions.Create(Options.DefaultName);
        }
    }

    private sealed class DefaultConfigureNamedOptions<T> : IConfigureNamedOptions<T>
        where T : class, new()
    {
        public void Configure(T options)
        {

        }
        public void Configure(string? name, T options)
        {

        }
    }
    private sealed class DefaultPostConfigureOptions<T> : IPostConfigureOptions<T>
        where T : class, new()
    {
        public void PostConfigure(string? name, T options)
        {

        }
    }

    private sealed class DefaultValidateOptions<T> : IValidateOptions<T>
        where T : class, new()
    {
        public ValidateOptionsResult Validate(string? name, T options)
        {
            return ValidateOptionsResult.Success;
        }
    }

    private sealed class DefaultChangeToken : IChangeToken
    {
        public static readonly DefaultChangeToken Instance = new();

        private sealed class Dis : IDisposable
        {
            public static readonly Dis DisInstance = new();
            public void Dispose(){}
        }

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
        {
            return Dis.DisInstance;
        }

        public bool HasChanged => false;
        public bool ActiveChangeCallbacks => true;
    }

    private sealed class DefaultOptionsChangeTokenSource<TOptions> : IOptionsChangeTokenSource<TOptions>
    {
        public IChangeToken GetChangeToken()
        {
            return DefaultChangeToken.Instance;
        }

        public string? Name => null;
    }

    public static void AddOptions(this ContainerBuilder builder)
    {
        builder.RegisterGeneric(typeof(OptionsWrapper<>)).As(typeof(IOptions<>));
        builder.RegisterGeneric(typeof(DefaultConfigureNamedOptions<>))
            .As(typeof(IConfigureOptions<>))
            .As(typeof(IConfigureNamedOptions<>));
        builder.RegisterGeneric(typeof(DefaultOptionsChangeTokenSource<>)).As(typeof(IOptionsChangeTokenSource<>));
        builder.RegisterGeneric(typeof(DefaultPostConfigureOptions<>)).As(typeof(IPostConfigureOptions<>));
        builder.RegisterGeneric(typeof(DefaultValidateOptions<>)).As(typeof(IValidateOptions<>));
        builder.RegisterGeneric(typeof(OptionsCache<>)).As(typeof(IOptionsMonitorCache<>));
        builder.RegisterGeneric(typeof(OptionsFactory<>)).As(typeof(IOptionsFactory<>));
        builder.RegisterGeneric(typeof(OptionsMonitor<>)).As(typeof(IOptionsMonitor<>));
    }

    public static void Configure<TOptions>(
        this ContainerBuilder builder,
        string? name,
        Action<TOptions> configure)
        where TOptions : class, new()
    {
        builder.Register<IConfigureNamedOptions<TOptions>>((_) =>
        {
            return new ConfigureNamedOptions<TOptions>(name, configure);
        })
            .As<IConfigureOptions<TOptions>>()
            .SingleInstance();
    }

    public static void Configure<TOptions, TUserObject>(
        this ContainerBuilder builder,
        string? name,
        Action<TOptions, TUserObject> configure)
        where TOptions : class, new()
        where TUserObject : class
    {
        builder.Register<IConfigureNamedOptions<TOptions>>((context) =>
        {
            return new ConfigureNamedOptions<TOptions, TUserObject>(name, context.Resolve<TUserObject>(), configure);
        })
            .As<IConfigureOptions<TOptions>>()
            .SingleInstance();
    }

    public static void PostConfigure<TOptions, TUserObject>(
        this ContainerBuilder builder,
        string? name,
        Action<TOptions, TUserObject> configure)
        where TOptions : class, new()
        where TUserObject : class
    {
        builder.Register<IPostConfigureOptions<TOptions>>((context) =>
        {
            return new PostConfigureOptions<TOptions, TUserObject>(name, context.Resolve<TUserObject>(), configure);
        })
            .As<IPostConfigureOptions<TOptions>>()
            .SingleInstance();
    }
    public static void PostConfigure<TOptions>(
        this ContainerBuilder builder,
        string? name,
        Action<TOptions> configure)
        where TOptions : class, new()
    {
        builder.Register<IPostConfigureOptions<TOptions>>((_) =>
        {
            return new PostConfigureOptions<TOptions>(name, configure);
        })
            .As<IPostConfigureOptions<TOptions>>()
            .SingleInstance();
    }

    public static void ValidateOptions<TOptions, TUserObject>(
        this ContainerBuilder builder,
        string? name,
        Func<TOptions, TUserObject, bool> configure,
        string failureMessage)
        where TOptions : class, new()
        where TUserObject : class
    {
        builder.Register<IValidateOptions<TOptions>>((context) =>
        {
            return new ValidateOptions<TOptions, TUserObject>(name, context.Resolve<TUserObject>(), configure, failureMessage);
        })
            .As<IValidateOptions<TOptions>>()
            .SingleInstance();
    }
    public static void ValidateOptions<TOptions>(
        this ContainerBuilder builder,
        string? name,
        Func<TOptions, bool> configure,
        string failureMessage)
        where TOptions : class, new()
    {
        builder.Register<IValidateOptions<TOptions>>((_) =>
        {
            return new ValidateOptions<TOptions>(name, configure, failureMessage);
        })
            .As<IValidateOptions<TOptions>>()
            .SingleInstance();
    }
}
