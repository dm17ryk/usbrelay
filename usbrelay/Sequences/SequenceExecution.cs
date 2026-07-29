using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace usbrelay.Sequences
{
    public sealed class SequenceRunResult
    {
        public SequenceRunResult(bool success, IEnumerable<string> log, Exception error)
            : this(success, log, error, exited: false)
        {
        }

        public SequenceRunResult(bool success, IEnumerable<string> log, Exception error, bool exited)
        {
            Success = success;
            Log = new List<string>(log);
            Error = error;
            Exited = exited;
        }

        public bool Success { get; }
        public IReadOnlyList<string> Log { get; }
        public Exception Error { get; }
        public bool Exited { get; }
        public string StatusText => Exited ? "stopped" : Success ? "finished" : "failed";
    }

    public sealed class SequenceExecutionContext
    {
        private readonly Dictionary<string, bool> booleanVariables = new Dictionary<string, bool>(StringComparer.Ordinal);

        public SequenceExecutionContext(
            ISequenceRelayBackend relay,
            IExternalToolRunner toolRunner,
            bool skipDelays,
            Action actionCompleted,
            Func<string, string, bool> confirmation = null)
        {
            Relay = relay;
            ToolRunner = toolRunner;
            SkipDelays = skipDelays;
            ActionCompleted = actionCompleted;
            Confirmation = confirmation;
            Log = new List<string>();
        }

        public ISequenceRelayBackend Relay { get; }
        public IExternalToolRunner ToolRunner { get; }
        public bool SkipDelays { get; }
        public Action ActionCompleted { get; }
        public Func<string, string, bool> Confirmation { get; }
        public List<string> Log { get; }
        public ExternalToolResult LastToolResult { get; set; }
        public bool ExitRequested { get; private set; }

        public void SetBooleanVariable(string name, bool value)
        {
            booleanVariables[name] = value;
        }

        public bool GetBooleanVariable(string name)
        {
            bool value;
            if (!booleanVariables.TryGetValue(name, out value))
                throw new InvalidOperationException("Sequence boolean variable is not defined: " + name);

            return value;
        }

        public bool Confirm(string title, string message)
        {
            if (Confirmation == null)
            {
                Log.Add("confirm \"" + title + "\": auto-accepted (CLI)");
                return true;
            }

            bool confirmed = Confirmation(title, message);
            Log.Add("confirm \"" + title + "\": " + (confirmed ? "OK" : "Cancel"));
            return confirmed;
        }

        public void Exit(string message)
        {
            Log.Add("EXIT: " + (message ?? string.Empty));
            ExitRequested = true;
        }

        public void NotifyActionCompleted()
        {
            if (ActionCompleted != null)
                ActionCompleted();
        }
    }

    public interface ISequenceRelayBackend
    {
        void SetChannel(string serialNumber, int channel, bool on);
        void SetChannel(string deviceSelector, string channelName, bool on);
        bool GetChannelState(string serialNumber, int channel);
        bool GetChannelState(string deviceSelector, string channelName);
    }

    public interface ISequenceRelayDisplay
    {
        string DescribeChannel(string deviceSelector, int channel);
        string DescribeChannel(string deviceSelector, string channelName);
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
        public static SequenceRunResult Run(
            SequenceParseResult sequence,
            ISequenceRelayBackend relay,
            IExternalToolRunner toolRunner,
            bool skipDelays = true,
            Action actionCompleted = null,
            Func<string, string, bool> confirmation = null)
        {
            var context = new SequenceExecutionContext(relay, toolRunner, skipDelays, actionCompleted, confirmation);

            if (!sequence.IsValid)
                return new SequenceRunResult(false, sequence.Diagnostics, new InvalidOperationException("Sequence is invalid."));

            try
            {
                foreach (var action in sequence.Actions)
                {
                    action.Execute(context);
                    if (context.ExitRequested)
                        return new SequenceRunResult(true, context.Log, null, exited: true);

                    context.NotifyActionCompleted();
                }

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
