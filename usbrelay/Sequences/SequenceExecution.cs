using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace usbrelay.Sequences
{
    public sealed class SequenceRunResult
    {
        public SequenceRunResult(bool success, IEnumerable<string> log, Exception error)
        {
            Success = success;
            Log = new List<string>(log);
            Error = error;
        }

        public bool Success { get; }
        public IReadOnlyList<string> Log { get; }
        public Exception Error { get; }
    }

    public sealed class SequenceExecutionContext
    {
        public SequenceExecutionContext(ISequenceRelayBackend relay, IExternalToolRunner toolRunner, bool skipDelays)
        {
            Relay = relay;
            ToolRunner = toolRunner;
            SkipDelays = skipDelays;
            Log = new List<string>();
        }

        public ISequenceRelayBackend Relay { get; }
        public IExternalToolRunner ToolRunner { get; }
        public bool SkipDelays { get; }
        public List<string> Log { get; }
        public ExternalToolResult LastToolResult { get; set; }
    }

    public interface ISequenceRelayBackend
    {
        void SetChannel(string serialNumber, int channel, bool on);
        bool GetChannelState(string serialNumber, int channel);
    }

    public interface IExternalToolRunner
    {
        ExternalToolResult Run(string path, string arguments);
    }

    public sealed class ExternalToolResult
    {
        public ExternalToolResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output ?? string.Empty;
        }

        public int ExitCode { get; }
        public string Output { get; }
    }

    public static class SequenceRunner
    {
        public static SequenceRunResult Run(SequenceParseResult sequence, ISequenceRelayBackend relay, IExternalToolRunner toolRunner, bool skipDelays = true)
        {
            var context = new SequenceExecutionContext(relay, toolRunner, skipDelays);

            if (!sequence.IsValid)
                return new SequenceRunResult(false, sequence.Diagnostics, new InvalidOperationException("Sequence is invalid."));

            try
            {
                foreach (var action in sequence.Actions)
                    action.Execute(context);

                return new SequenceRunResult(true, context.Log, null);
            }
            catch (Exception ex)
            {
                context.Log.Add("ERROR: " + ex.Message);
                return new SequenceRunResult(false, context.Log, ex);
            }
        }
    }

    public sealed class ProcessExternalToolRunner : IExternalToolRunner
    {
        public ExternalToolResult Run(string path, string arguments)
        {
            var startInfo = new ProcessStartInfo(path, arguments)
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            var output = new StringBuilder();
            object outputLock = new object();

            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (sender, e) => AppendOutput(output, outputLock, e.Data);
                process.ErrorDataReceived += (sender, e) => AppendOutput(output, outputLock, e.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                return new ExternalToolResult(process.ExitCode, output.ToString());
            }
        }

        private static void AppendOutput(StringBuilder output, object outputLock, string line)
        {
            if (line == null)
                return;

            lock (outputLock)
            {
                output.AppendLine(line);
            }
        }
    }
}
