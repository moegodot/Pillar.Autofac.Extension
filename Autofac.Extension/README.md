
The document is simple,source code may help.
Any question,please open an issue to tell me.

IMPORTANT: Sample/Program.cs is useful

## Logging

See LoggingHelper.cs file,it it self-evident.

## Configuration

supports:
 - IOptions\<TOptions>
 - IOptionsMonitor\<TOptions>

IOptions\<TOptions> works anyhow

IOptionsMonitor\<TOptions> works only when the ExtendedHostBuilder.BindOptions\<TOptions> is called

Configure them by Autofac.ContainerBuilder.Configure() PostConfigure() or Validate() methods.

Do not forget to call AddOptions()

## Extended Host
It is a more powerful version of the .NET Core Host.

ExtendedHost has a method named StartInCurrentThread to run the Start() in current thread.

This may be useful when it comes to UI applications,when [STAThread] is required.
