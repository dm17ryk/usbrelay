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
        private readonly string channelName;
        private readonly bool on;

        public RelayAction(string serialNumber, int channel, bool on)
        {
            this.serialNumber = serialNumber;
            this.channel = channel;
            channelName = null;
            this.on = on;
        }

        public RelayAction(string deviceSelector, string namedChannel, bool on)
        {
            serialNumber = deviceSelector;
            channelName = namedChannel;
            channel = 0;
            this.on = on;
        }

        public IEnumerable<RelayResource> Resources => string.IsNullOrEmpty(channelName)
            ? new[] { new RelayResource(serialNumber, channel) }
            : new[] { new RelayResource(serialNumber, channelName) };

        public void Execute(SequenceExecutionContext context)
        {
            if (string.IsNullOrEmpty(channelName))
                context.Relay.SetChannel(serialNumber, channel, on);
            else
                context.Relay.SetChannel(serialNumber, channelName, on);
            context.Log.Add(SequenceLog.Channel(context, serialNumber, channel, channelName) + " -> " + (on ? "ON" : "OFF") + " ok");
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
        private readonly string channelName;
        private readonly RelayState expectedState;
        private readonly int timeoutMilliseconds;

        public WaitChannelAction(string serialNumber, int channel, RelayState expectedState, int timeoutMilliseconds)
        {
            this.serialNumber = serialNumber;
            this.channel = channel;
            channelName = null;
            this.expectedState = expectedState;
            this.timeoutMilliseconds = timeoutMilliseconds;
        }

        public WaitChannelAction(string deviceSelector, string namedChannel, RelayState expectedState, int timeoutMilliseconds)
        {
            serialNumber = deviceSelector;
            channelName = namedChannel;
            channel = 0;
            this.expectedState = expectedState;
            this.timeoutMilliseconds = timeoutMilliseconds;
        }

        public IEnumerable<RelayResource> Resources => string.IsNullOrEmpty(channelName)
            ? new[] { new RelayResource(serialNumber, channel) }
            : new[] { new RelayResource(serialNumber, channelName) };

        public void Execute(SequenceExecutionContext context)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            bool expected = expectedState == RelayState.On;
            string label = SequenceLog.Channel(context, serialNumber, channel, channelName);

            while (DateTime.UtcNow <= deadline)
            {
                bool isOn = string.IsNullOrEmpty(channelName)
                    ? context.Relay.GetChannelState(serialNumber, channel)
                    : context.Relay.GetChannelState(serialNumber, channelName);
                if (isOn == expected)
                {
                    context.Log.Add("wait " + label + " " + expectedState + " ok");
                    return;
                }

                if (context.SkipDelays)
                    break;

                Thread.Sleep(50);
            }

            throw new InvalidOperationException("Timed out waiting for " + label + " " + expectedState);
        }
    }

    internal sealed class ReadChannelAction : ISequenceAction
    {
        private readonly string serialNumber;
        private readonly int channel;
        private readonly string channelName;

        public ReadChannelAction(string serialNumber, int channel)
        {
            this.serialNumber = serialNumber;
            this.channel = channel;
            channelName = null;
        }

        public ReadChannelAction(string deviceSelector, string namedChannel)
        {
            serialNumber = deviceSelector;
            channelName = namedChannel;
            channel = 0;
        }

        public IEnumerable<RelayResource> Resources => string.IsNullOrEmpty(channelName)
            ? new[] { new RelayResource(serialNumber, channel) }
            : new[] { new RelayResource(serialNumber, channelName) };

        public void Execute(SequenceExecutionContext context)
        {
            bool on = string.IsNullOrEmpty(channelName)
                ? context.Relay.GetChannelState(serialNumber, channel)
                : context.Relay.GetChannelState(serialNumber, channelName);
            context.Log.Add(SequenceLog.Channel(context, serialNumber, channel, channelName) + " read " + (on ? "ON" : "OFF"));
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

    internal sealed class ConfirmAction : ISequenceAction
    {
        private readonly string variableName;
        private readonly string title;
        private readonly string message;

        public ConfirmAction(string variableName, string title, string message)
        {
            this.variableName = variableName;
            this.title = title;
            this.message = message;
        }

        public IEnumerable<RelayResource> Resources => new RelayResource[0];

        public void Execute(SequenceExecutionContext context)
        {
            bool confirmed = context.Confirm(title, message);
            context.SetBooleanVariable(variableName, confirmed);
            context.Log.Add("stored boolean variable " + variableName + " = " + confirmed);
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

    internal sealed class IfBooleanVariableAction : ISequenceAction
    {
        private readonly string variableName;
        private readonly bool negated;
        private readonly IReadOnlyList<ISequenceAction> successActions;
        private readonly IReadOnlyList<ISequenceAction> failureActions;

        public IfBooleanVariableAction(
            string variableName,
            bool negated,
            IReadOnlyList<ISequenceAction> successActions,
            IReadOnlyList<ISequenceAction> failureActions)
        {
            this.variableName = variableName;
            this.negated = negated;
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
            bool value = context.GetBooleanVariable(variableName);
            bool condition = negated ? !value : value;
            context.Log.Add(
                "if " + (negated ? "!" : string.Empty) + variableName + ": "
                + (condition ? "success" : "failure"));

            foreach (var action in condition ? successActions : failureActions)
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

    internal sealed class ExitAction : ISequenceAction
    {
        private readonly string message;

        public ExitAction(string message)
        {
            this.message = message;
        }

        public IEnumerable<RelayResource> Resources => new RelayResource[0];

        public void Execute(SequenceExecutionContext context)
        {
            context.Exit(message);
        }
    }

    internal static class SequenceLog
    {
        public static string Channel(SequenceExecutionContext context, string deviceSelector, int channel, string channelName)
        {
            var display = context.Relay as ISequenceRelayDisplay;
            if (display != null)
            {
                try
                {
                    return string.IsNullOrEmpty(channelName)
                        ? display.DescribeChannel(deviceSelector, channel)
                        : display.DescribeChannel(deviceSelector, channelName);
                }
                catch (Exception)
                {
                    // The operation itself reports the authoritative device/channel error.
                }
            }

            return deviceSelector + " " + (string.IsNullOrEmpty(channelName) ? "CH" + channel : channelName);
        }
    }
}
