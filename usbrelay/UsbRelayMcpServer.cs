using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using usbrelay.Sequences;

namespace usbrelay
{
    public static class UsbRelayMcpServer
    {
        public static int Run()
        {
            var error = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true };
            Console.SetError(TextWriter.Synchronized(error));
            var trace = new TextWriterTraceListener(Console.Error);
            Trace.Listeners.Add(trace);
            Trace.AutoFlush = true;
            using (var shutdown = new CancellationTokenSource())
            using (var tools = new UsbRelayMcpTools(
                new RelayService(new NativeUsbRelayBackend()),
                new SequenceRepository(SequenceRepository.DefaultPath),
                new NativeUsbRelayBackend(), Console.Error))
            using (var loggerFactory = new StderrLoggerFactory(Console.Error))
            {
                ConsoleCancelEventHandler cancel = (sender, args) => { args.Cancel = true; shutdown.Cancel(); };
                Console.CancelKeyPress += cancel;
                McpServer server = null;
                try
                {
                    var options = new McpServerOptions
                    {
                        ServerInfo = new Implementation { Name = "usbrelay", Version = typeof(UsbRelayMcpServer).Assembly.GetName().Version.ToString() },
                        ServerInstructions = "Control locally connected Windows USB relay boards. Start with relay_list to discover full device paths and channel names. "
                            + "Use full paths when serial numbers are duplicated. Switching relays changes physical equipment. "
                            + "Saved sequences may run external programs. Sequence confirmations follow the confirm argument. GUI, CLI and MCP share saved names and sequences.",
                        ToolCollection = new McpServerPrimitiveCollection<McpServerTool>()
                    };
                    foreach (MethodInfo method in typeof(UsbRelayMcpTools).GetMethods().Where(method => method.IsDefined(typeof(McpServerToolAttribute), false)))
                        options.ToolCollection.Add(McpServerTool.Create(method, tools));
                    Console.Error.WriteLine("[MCP] Starting usbrelay stdio server; tools=" + options.ToolCollection.Count);
                    server = McpServer.Create(new StdioServerTransport(options, loggerFactory), options, loggerFactory);
                    server.RunAsync(shutdown.Token).GetAwaiter().GetResult();
                    Console.Error.WriteLine("[MCP] Input closed; server stopped.");
                    return 0;
                }
                catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
                {
                    Console.Error.WriteLine("[MCP] Shutdown requested.");
                    return 0;
                }
                finally
                {
                    if (server != null) server.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    Console.CancelKeyPress -= cancel;
                    Trace.Listeners.Remove(trace);
                    trace.Dispose();
                }
            }
        }

        private sealed class StderrLoggerFactory : ILoggerFactory
        {
            private readonly TextWriter output;
            public StderrLoggerFactory(TextWriter output) { this.output = output; }
            public ILogger CreateLogger(string categoryName) => new StderrLogger(output, categoryName);
            public void AddProvider(ILoggerProvider provider) { }
            public void Dispose() { }
        }

        private sealed class StderrLogger : ILogger
        {
            private readonly TextWriter output;
            private readonly string category;
            public StderrLogger(TextWriter output, string category) { this.output = output; this.category = category; }
            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                output.WriteLine("[MCP " + logLevel + "] " + category + ": " + formatter(state, exception));
                if (exception != null) output.WriteLine(exception);
            }
        }
    }
}
