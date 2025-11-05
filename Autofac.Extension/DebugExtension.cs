using Autofac.Diagnostics.DotGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autofac.Extension;

public static class DebugExtension
{
    public static void ConsoleDotGrapyOutput(string service, string dotGraphy)
    {
        var content = Uri.EscapeDataString(dotGraphy);
        var url = $"https://dreampuf.github.io/GraphvizOnline/?engine=dot#{content}";
        var graph = $"\x1B]8;;{url}\x1B\\graph\x1B]8;;\x1B\\";
        var withUnderLine = $"\x1B[4m{graph}\x1B[0m";
        var withBlueColor = $"\x1B[34m{withUnderLine}\x1B[0m";
        var output = $"Resolve {service}[{withBlueColor}]";
        Console.WriteLine(output);
    }

    public static void OutputDotGraph(this IContainer container, Action<string, string>? output = null)
    {
        output = output ?? ConsoleDotGrapyOutput;
        var tracer = new DotDiagnosticTracer();
        tracer.OperationCompleted += (sender, args) =>
        {
            if (args.Operation.InitiatingRequest is not null)
            {
                output(
                    args.Operation.InitiatingRequest.Value.Service.ToString(),
                    args.TraceContent);
            }
        };
        container.SubscribeToDiagnostics(tracer);
    }
}
