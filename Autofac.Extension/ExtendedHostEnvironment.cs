using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Autofac.Extension;

public sealed class ExtendedHostEnvironment : IHostEnvironment
{
    public ExtendedHostEnvironment(string applicationName, string contentRootPath, string environmentName)
    {
        if (environmentName != Environments.Development &&
           environmentName != Environments.Production &&
           environmentName != Environments.Staging)
        {
            throw new ArgumentException($"Invalid environment name: {environmentName}," +
                $"valid value see:Microsoft.Extensions.Hosting.Environments");
        }

        EnvironmentName = environmentName;
        ApplicationName = applicationName;
        ContentRootPath = contentRootPath;
        ContentRootFileProvider = new PhysicalFileProvider(Path.GetFullPath(contentRootPath));
    }

    public string EnvironmentName { get; set; }

    public string ApplicationName { get; set; }

    public string ContentRootPath { get; set; }

    public IFileProvider ContentRootFileProvider { get; set; }

}
