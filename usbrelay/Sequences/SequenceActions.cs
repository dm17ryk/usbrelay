using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace usbrelay.Sequences
{
    internal sealed class RelayAction : ISequenceAction
    {
        private readonly string serialNumber;
        private readonly int channel;
        private readonly bool on;

        public RelayAction(string serialNumber, int channel, bool on)
        {
            this.serialNumber = serialNumber;
            this.channel = channel;
            this.on = on;
        }

        public IEnumerable<RelayResource> Resources => new[] { new RelayResource(serialNumber, channel) };

        public void Execute(SequenceExecutionContext context)
        {
            context.Relay.SetChannel(serialNumber, channel, on);
            context.Log.Add(serialNumber + " CH" + channel + " -> " + (on ? "ON" : "OFF") + " ok");
        }
    }

    internal sealed class SleepAction : ISequenceAction
    {
        private readonly int milliseconds;

        public SleepAction(int milliseconds)
        {
            this.milliseconds = milliseconds;
        }

        public IEnumerable<RelayResource> Resources => new RelayResource[0];

        public void Execute(SequenceExecutionContext context)
        {
            context.Log.Add("sleep " + milliseconds + " ms");
            if (!context.SkipDelays)
                Thread.Sleep(milliseconds);
        }
    }

    internal sealed class WaitChannelAction : ISequenceAction
    {
        private readonly string serialNumber;
        private readonly int channel;
        private readonly RelayState expectedState;
        private readonly int timeoutMilliseconds;

        public WaitChannelAction(string serialNumber, int channel, RelayState expectedState, int timeoutMilliseconds)
        {
            this.serialNumber = serialNumber;
            this.channel = channel;
            this.expectedState = expectedState;
            this.timeoutMilliseconds = timeoutMilliseconds;
        }

        public IEnumerable<RelayResource> Resources => new[] { new RelayResource(serialNumber, channel) };

        public void Execute(SequenceExecutionContext context)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            bool expected = expectedState == RelayState.On;

            while (DateTime.UtcNow <= deadline)
            {
                if (context.Relay.GetChannelState(serialNumber, channel) == expected)
                {
                    context.Log.Add("wait " + serialNumber + " CH" + channel + " " + expectedState + " ok");
                    return;
                }

                if (context.SkipDelays)
                    break;

                Thread.Sleep(50);
            }

            throw new InvalidOperationException("Timed out waiting for " + serialNumber + " CH" + channel + " " + expectedState);
        }
    }

    internal sealed class ReadChannelAction : ISequenceAction
    {
        private readonly string serialNumber;
        private readonly int channel;

        public ReadChannelAction(string serialNumber, int channel)
        {
            this.serialNumber = serialNumber;
            this.channel = channel;
        }

        public IEnumerable<RelayResource> Resources => new[] { new RelayResource(serialNumber, channel) };

        public void Execute(SequenceExecutionContext context)
        {
            bool on = context.Relay.GetChannelState(serialNumber, channel);
            context.Log.Add(serialNumber + " CH" + channel + " read " + (on ? "ON" : "OFF"));
        }
    }

    internal sealed class RunToolAction : ISequenceAction
    {
        private readonly string path;
        private readonly string arguments;

        public RunToolAction(string path, string arguments)
        {
            this.path = path;
            this.arguments = arguments;
        }

        public IEnumerable<RelayResource> Resources => new RelayResource[0];

        public void Execute(SequenceExecutionContext context)
        {
            context.LastToolResult = context.ToolRunner.Run(path, arguments);
            context.Log.Add("tool " + path + " exited " + context.LastToolResult.ExitCode);
        }
    }

    internal sealed class IfLastToolOutputMatchesAction : ISequenceAction
    {
        private readonly string pattern;
        private readonly IReadOnlyList<ISequenceAction> successActions;
        private readonly IReadOnlyList<ISequenceAction> failureActions;

        public IfLastToolOutputMatchesAction(
            string pattern,
            IReadOnlyList<ISequenceAction> successActions,
            IReadOnlyList<ISequenceAction> failureActions)
        {
            this.pattern = pattern;
            this.successActions = successActions;
            this.failureActions = failureActions;
        }

        public IEnumerable<RelayResource> Resources
        {
            get
            {
                foreach (var action in successActions)
                    foreach (var resource in action.Resources)
                        yield return resource;
                foreach (var action in failureActions)
                    foreach (var resource in action.Resources)
                        yield return resource;
            }
        }

        public void Execute(SequenceExecutionContext context)
        {
            string output = context.LastToolResult == null ? string.Empty : context.LastToolResult.Output;
            bool matched = Regex.IsMatch(output, pattern);
            context.Log.Add("OutputMatches " + pattern + ": " + (matched ? "success" : "failure"));

            foreach (var action in matched ? successActions : failureActions)
                action.Execute(context);
        }
    }

    internal sealed class FailAction : ISequenceAction
    {
        private readonly string message;

        public FailAction(string message)
        {
            this.message = message;
        }

        public IEnumerable<RelayResource> Resources => new RelayResource[0];

        public void Execute(SequenceExecutionContext context)
        {
            throw new InvalidOperationException(message);
        }
    }
}
