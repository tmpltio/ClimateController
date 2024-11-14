using ClimateController.Configuration;
using ClimateController.Home;
using Microsoft.Extensions.Logging;

namespace ClimateController;

internal static class Program
{
    public static async Task Main()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
            });
            builder.SetMinimumLevel(Manager.LogLevel);
        });
        var cancellationTokenSource = new CancellationTokenSource();

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            cancellationTokenSource.Cancel();

        var controller = Controller.Build(loggerFactory);
        await controller.RunAsync(cancellationTokenSource.Token);

        loggerFactory
            .CreateLogger("Program")
            .LogCritical("Unexpected end of one of tasks, finishing...");
    }
}