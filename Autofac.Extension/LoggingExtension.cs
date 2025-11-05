using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autofac.Extension;
public static class LoggingExtension
{
    /// <summary>
    /// This enable Microsoft.Extensions.Logging in Autofac.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    public static void AddLogging(this ContainerBuilder builder, Action<ILoggingBuilder> configure)
    {
        var factory = LoggerFactory.Create(configure);
        builder.RegisterInstance(factory).As<ILoggerFactory>().SingleInstance();
        builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>)).SingleInstance();
    }

    /// <summary>
    /// This is suitable when you use your logging framework
    /// </summary>
    public static void AddILoggerOnly(this ContainerBuilder builder)
    {
        builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>)).SingleInstance();
    }
}
