using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace usbrelay
{
    public sealed class SequenceCompletionItem
    {
        public SequenceCompletionItem(string text, string description)
        {
            Text = text;
            Description = description;
        }

        public string Text { get; }
        public string Description { get; }
    }

    public sealed class SequenceCompletionResult
    {
        public SequenceCompletionResult(int replacementStart, int replacementLength, IEnumerable<SequenceCompletionItem> items)
        {
            ReplacementStart = replacementStart;
            ReplacementLength = replacementLength;
            Items = items.ToArray();
        }

        public int ReplacementStart { get; }
        public int ReplacementLength { get; }
        public IReadOnlyList<SequenceCompletionItem> Items { get; }
    }

    public static class SequenceCompletionProvider
    {
        private static readonly Regex ToolVariableRegex = new Regex(
            @"\bvar\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*sequence\.RunTool\s*\(",
            RegexOptions.Compiled);

        public static SequenceCompletionResult GetCompletions(string text, int caretOffset, IEnumerable<RelayDevice> devices, bool force)
        {
            text = text ?? string.Empty;
            caretOffset = Math.Max(0, Math.Min(caretOffset, text.Length));

            MemberAccess memberAccess = FindMemberAccess(text, caretOffset);
            if (memberAccess.HasTarget)
            {
                IEnumerable<SequenceCompletionItem> items = GetMemberItems(memberAccess.Target, text, memberAccess.DotOffset, devices);
                return new SequenceCompletionResult(
                    memberAccess.ReplacementStart,
                    memberAccess.ReplacementLength,
                    FilterByPrefix(items, memberAccess.MemberPrefix));
            }

            if (!force)
                return new SequenceCompletionResult(caretOffset, 0, new SequenceCompletionItem[0]);

            int wordStart = FindCurrentWordStart(text, caretOffset);
            return new SequenceCompletionResult(
                wordStart,
                caretOffset - wordStart,
                GetTopLevelItems(text.Substring(0, caretOffset), devices));
        }

        private static IEnumerable<SequenceCompletionItem> GetMemberItems(string target, string text, int dotOffset, IEnumerable<RelayDevice> devices)
        {
            if (string.Equals(target, "sequence", StringComparison.Ordinal))
                return GetSequenceItems(devices, includeSequencePrefix: false);

            if (string.Equals(target, "RelayState", StringComparison.Ordinal))
            {
                return new[]
                {
                    new SequenceCompletionItem("Off", "Relay channel is off"),
                    new SequenceCompletionItem("On", "Relay channel is on")
                };
            }

            if (GetToolVariableNames(text.Substring(0, dotOffset)).Contains(target))
                return new[] { new SequenceCompletionItem("OutputMatches(\"READY|OK\")", "Test the last tool output with a regular expression") };

            return new SequenceCompletionItem[0];
        }

        private static IEnumerable<SequenceCompletionItem> GetTopLevelItems(string textBeforeCaret, IEnumerable<RelayDevice> devices)
        {
            foreach (SequenceCompletionItem item in GetSequenceItems(devices, includeSequencePrefix: true))
                yield return item;

            RelayTarget branchTarget = GetRelayTargets(devices).First();
            foreach (string variable in GetToolVariableNames(textBeforeCaret))
            {
                yield return new SequenceCompletionItem(
                    "if (" + variable + ".OutputMatches(\"READY|OK\")) {" + Environment.NewLine +
                    "    sequence.PowerOn(\"" + EscapeString(branchTarget.SerialNumber) + "\", " + branchTarget.Channel + ");" + Environment.NewLine +
                    "} else {" + Environment.NewLine +
                    "    sequence.Fail(\"Tool output did not match\");" + Environment.NewLine +
                    "}",
                    "Branch on " + variable + " output using a regular expression");
            }
        }

        private static IEnumerable<SequenceCompletionItem> GetSequenceItems(IEnumerable<RelayDevice> devices, bool includeSequencePrefix)
        {
            string prefix = includeSequencePrefix ? "sequence." : string.Empty;
            string suffix = includeSequencePrefix ? ";" : string.Empty;

            foreach (RelayTarget target in GetRelayTargets(devices))
            {
                string channel = "\"" + EscapeString(target.SerialNumber) + "\", " + target.Channel;
                yield return new SequenceCompletionItem(prefix + "PowerOn(" + channel + ")" + suffix, "Turn " + target.SerialNumber + " CH" + target.Channel + " on");
                yield return new SequenceCompletionItem(prefix + "PowerOff(" + channel + ")" + suffix, "Turn " + target.SerialNumber + " CH" + target.Channel + " off");
                yield return new SequenceCompletionItem(prefix + "ReadChannel(" + channel + ")" + suffix, "Read " + target.SerialNumber + " CH" + target.Channel + " status");
                yield return new SequenceCompletionItem(prefix + "WaitChannel(" + channel + ", RelayState.On, 3000)" + suffix, "Wait for " + target.SerialNumber + " CH" + target.Channel + " to turn on");
                yield return new SequenceCompletionItem(prefix + "WaitChannel(" + channel + ", RelayState.Off, 3000)" + suffix, "Wait for " + target.SerialNumber + " CH" + target.Channel + " to turn off");
            }

            yield return new SequenceCompletionItem(prefix + "Sleep(500)" + suffix, "Pause sequence execution");
            yield return new SequenceCompletionItem(prefix + "RunTool(\"tool.exe\", \"--args\")" + suffix, "Run an external process and wait for exit");
            yield return new SequenceCompletionItem(prefix + "Fail(\"message\")" + suffix, "Fail the current sequence");
        }

        private static IEnumerable<SequenceCompletionItem> FilterByPrefix(IEnumerable<SequenceCompletionItem> items, string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return items;

            return items.Where(item => item.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> GetToolVariableNames(string text)
        {
            var names = new List<string>();
            foreach (Match match in ToolVariableRegex.Matches(text ?? string.Empty))
                names.Add(match.Groups["name"].Value);
            return names;
        }

        private const string DefaultSerial = "6QMBS";

        private static IEnumerable<RelayTarget> GetRelayTargets(IEnumerable<RelayDevice> devices)
        {
            var targets = new List<RelayTarget>();
            if (devices != null)
            {
                foreach (RelayDevice device in devices)
                {
                    if (device == null || device.ChannelCount <= 0)
                        continue;

                    for (int channel = 1; channel <= device.ChannelCount; channel++)
                        targets.Add(new RelayTarget(device.SerialNumber, channel));
                }
            }

            if (targets.Count == 0)
                targets.Add(new RelayTarget(DefaultSerial, 1));

            return targets;
        }

        private static MemberAccess FindMemberAccess(string text, int caretOffset)
        {
            int replacementStart = FindCurrentWordStart(text, caretOffset);
            if (replacementStart == 0 || text[replacementStart - 1] != '.')
                return MemberAccess.None;

            int targetEnd = replacementStart - 1;
            int targetStart = targetEnd;
            while (targetStart > 0 && IsIdentifierPart(text[targetStart - 1]))
                targetStart--;

            if (targetStart == targetEnd)
                return MemberAccess.None;

            return new MemberAccess(
                text.Substring(targetStart, targetEnd - targetStart),
                replacementStart,
                caretOffset - replacementStart,
                text.Substring(replacementStart, caretOffset - replacementStart),
                targetEnd);
        }

        private static int FindCurrentWordStart(string text, int caretOffset)
        {
            int index = caretOffset;
            while (index > 0 && IsIdentifierPart(text[index - 1]))
                index--;
            return index;
        }

        private static bool IsIdentifierPart(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static string EscapeString(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class RelayTarget
        {
            public RelayTarget(string serialNumber, int channel)
            {
                SerialNumber = serialNumber;
                Channel = channel;
            }

            public string SerialNumber { get; }
            public int Channel { get; }
        }

        private struct MemberAccess
        {
            public static readonly MemberAccess None = new MemberAccess(null, 0, 0, null, 0);

            public MemberAccess(string target, int replacementStart, int replacementLength, string memberPrefix, int dotOffset)
            {
                Target = target;
                ReplacementStart = replacementStart;
                ReplacementLength = replacementLength;
                MemberPrefix = memberPrefix;
                DotOffset = dotOffset;
            }

            public bool HasTarget => Target != null;
            public string Target { get; }
            public int ReplacementStart { get; }
            public int ReplacementLength { get; }
            public string MemberPrefix { get; }
            public int DotOffset { get; }
        }
    }
}
