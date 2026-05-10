using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace usbrelay.Sequences
{
    public static class SequenceParser
    {
        private static readonly Regex PowerRegex = new Regex(@"^sequence\.Power(?<state>On|Off)\(""(?<serial>[^""]+)""\s*,\s*(?<channel>\d+)\);?$", RegexOptions.Compiled);
        private static readonly Regex SleepRegex = new Regex(@"^sequence\.Sleep\((?<ms>\d+)\);?$", RegexOptions.Compiled);
        private static readonly Regex ReadRegex = new Regex(@"^sequence\.ReadChannel\(""(?<serial>[^""]+)""\s*,\s*(?<channel>\d+)\);?$", RegexOptions.Compiled);
        private static readonly Regex WaitRegex = new Regex(@"^sequence\.WaitChannel\(""(?<serial>[^""]+)""\s*,\s*(?<channel>\d+)\s*,\s*RelayState\.(?<state>On|Off)\s*,\s*(?<timeout>\d+)\);?$", RegexOptions.Compiled);
        private static readonly Regex RunToolRegex = new Regex(@"^(var\s+\w+\s*=\s*)?sequence\.RunTool\(""(?<path>[^""]+)""\s*,\s*""(?<args>[^""]*)""\);?$", RegexOptions.Compiled);
        private static readonly Regex IfRegex = new Regex(@"^if\s*\(\s*\w+\.OutputMatches\(""(?<pattern>[^""]+)""\)\s*\)\s*\{?$", RegexOptions.Compiled);
        private static readonly Regex FailRegex = new Regex(@"^sequence\.Fail\(""(?<message>[^""]*)""\);?$", RegexOptions.Compiled);

        public static SequenceParseResult Parse(string script)
        {
            var diagnostics = new List<string>();
            diagnostics.AddRange(GetSyntaxDiagnostics(script));
            var lines = Normalize(script);
            int index = 0;
            var actions = ParseBlock(lines, ref index, diagnostics, stopOnElseOrClose: false);

            if (index < lines.Count)
                diagnostics.Add("Unexpected token: " + lines[index]);

            return new SequenceParseResult(actions, diagnostics);
        }

        private static IEnumerable<string> GetSyntaxDiagnostics(string script)
        {
            string wrappedScript = "class SequenceScript { void Run() {" + Environment.NewLine + script + Environment.NewLine + "} }";
            SyntaxTree tree = CSharpSyntaxTree.ParseText(wrappedScript);
            foreach (Diagnostic diagnostic in tree.GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    yield return diagnostic.GetMessage();
            }
        }

        private static List<ISequenceAction> ParseBlock(IReadOnlyList<string> lines, ref int index, List<string> diagnostics, bool stopOnElseOrClose)
        {
            var actions = new List<ISequenceAction>();

            while (index < lines.Count)
            {
                string line = lines[index];

                if (line == "}" || line == "} else {" || line == "else {")
                {
                    if (stopOnElseOrClose)
                        break;

                    diagnostics.Add("Unexpected block delimiter: " + line);
                    index++;
                    continue;
                }

                Match match = PowerRegex.Match(line);
                if (match.Success)
                {
                    actions.Add(new RelayAction(match.Groups["serial"].Value, Int(match, "channel"), match.Groups["state"].Value == "On"));
                    index++;
                    continue;
                }

                match = SleepRegex.Match(line);
                if (match.Success)
                {
                    actions.Add(new SleepAction(Int(match, "ms")));
                    index++;
                    continue;
                }

                match = WaitRegex.Match(line);
                if (match.Success)
                {
                    RelayState state = (RelayState)Enum.Parse(typeof(RelayState), match.Groups["state"].Value);
                    actions.Add(new WaitChannelAction(match.Groups["serial"].Value, Int(match, "channel"), state, Int(match, "timeout")));
                    index++;
                    continue;
                }

                match = ReadRegex.Match(line);
                if (match.Success)
                {
                    actions.Add(new ReadChannelAction(match.Groups["serial"].Value, Int(match, "channel")));
                    index++;
                    continue;
                }

                match = RunToolRegex.Match(line);
                if (match.Success)
                {
                    actions.Add(new RunToolAction(match.Groups["path"].Value, match.Groups["args"].Value));
                    index++;
                    continue;
                }

                match = IfRegex.Match(line);
                if (match.Success)
                {
                    index++;
                    var successActions = ParseBlock(lines, ref index, diagnostics, stopOnElseOrClose: true);
                    var failureActions = new List<ISequenceAction>();

                    if (index + 1 < lines.Count && lines[index] == "}" && lines[index + 1] == "else {")
                    {
                        index += 2;
                        failureActions = ParseBlock(lines, ref index, diagnostics, stopOnElseOrClose: true);
                    }
                    else if (index < lines.Count && (lines[index] == "} else {" || lines[index] == "else {"))
                    {
                        index++;
                        failureActions = ParseBlock(lines, ref index, diagnostics, stopOnElseOrClose: true);
                    }

                    if (index < lines.Count && lines[index] == "}")
                        index++;
                    else
                        diagnostics.Add("Missing closing brace for if block.");

                    actions.Add(new IfLastToolOutputMatchesAction(match.Groups["pattern"].Value, successActions, failureActions));
                    continue;
                }

                match = FailRegex.Match(line);
                if (match.Success)
                {
                    actions.Add(new FailAction(match.Groups["message"].Value));
                    index++;
                    continue;
                }

                diagnostics.Add("Unsupported sequence command: " + line);
                index++;
            }

            return actions;
        }

        private static List<string> Normalize(string script)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(script))
                return lines;

            foreach (string rawLine in script.Replace("} else {", "\n} else {\n").Replace("{", "{\n").Replace("}", "\n}\n").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = rawLine.Trim();
                if (line.Length > 0)
                    lines.Add(line);
            }

            return lines;
        }

        private static int Int(Match match, string groupName)
        {
            return Convert.ToInt32(match.Groups[groupName].Value);
        }
    }
}
