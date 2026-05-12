using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using System.Linq;

namespace usbrelay
{
    internal static class CliCompletion
    {
        public static IEnumerable<string> GetSuggestions(string commandLine, int cursorPosition)
        {
            CompletionInput input = CreateInput(commandLine, cursorPosition);
            CliGrammar grammar = CliGrammar.Current;
            ParseResult parseResult = grammar.RootCommand.Parse(input.ArgumentsText);

            var parserCompletions = parseResult
                .GetCompletions(input.CursorPosition)
                .Select(completion => completion.Label)
                .ToArray();

            IEnumerable<string> completions = parserCompletions;
            bool hasValueCompletions = false;
            if (!input.WordToComplete.StartsWith("-", StringComparison.Ordinal))
            {
                var valueCompletions = parserCompletions
                    .Where(completion => !completion.StartsWith("-", StringComparison.Ordinal) && !completion.StartsWith("/", StringComparison.Ordinal))
                    .ToArray();
                if (valueCompletions.Length > 0)
                {
                    completions = valueCompletions;
                    hasValueCompletions = true;
                }
            }

            if (!hasValueCompletions)
                completions = completions.Concat(GetOptionAliasCompletions(grammar.RootCommand, input.WordToComplete));

            return completions
                .Where(completion => StartsWith(completion, input.WordToComplete))
                .Distinct()
                .OrderBy(completion => completion);
        }

        private static CompletionInput CreateInput(string commandLine, int cursorPosition)
        {
            if (commandLine == null)
                commandLine = string.Empty;

            if (cursorPosition < 0)
                cursorPosition = 0;
            if (cursorPosition > commandLine.Length)
                cursorPosition = commandLine.Length;

            string prefix = commandLine.Substring(0, cursorPosition);
            bool endsWithWhitespace = prefix.Length > 0 && char.IsWhiteSpace(prefix[prefix.Length - 1]);
            List<string> tokens = CommandLineParser.SplitCommandLine(prefix).ToList();

            if (tokens.Count > 0 && !tokens[0].StartsWith("-", StringComparison.Ordinal))
                tokens.RemoveAt(0);

            string wordToComplete = endsWithWhitespace || tokens.Count == 0 ? string.Empty : tokens[tokens.Count - 1];
            string argumentsText = string.Join(" ", tokens.Select(EscapeToken));
            if (endsWithWhitespace && argumentsText.Length > 0)
                argumentsText += " ";

            return new CompletionInput(argumentsText, argumentsText.Length, wordToComplete);
        }

        private static string EscapeToken(string token)
        {
            if (token == null)
                return string.Empty;

            if (token.Length == 0)
                return "\"\"";

            if (!token.Any(char.IsWhiteSpace) && token.IndexOf('"') < 0)
                return token;

            return "\"" + token.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static IEnumerable<string> GetOptionAliasCompletions(Command command, string wordToComplete)
        {
            if (!string.IsNullOrEmpty(wordToComplete) && !wordToComplete.StartsWith("-", StringComparison.Ordinal))
                return Enumerable.Empty<string>();

            return command.Options
                .SelectMany(option => new[] { option.Name }.Concat(option.Aliases))
                .Where(IsOptionToken)
                .Where(alias => StartsWith(alias, wordToComplete));
        }

        private static bool IsOptionToken(string alias)
        {
            return !string.IsNullOrEmpty(alias)
                && (alias.StartsWith("-", StringComparison.Ordinal) || alias.StartsWith("/", StringComparison.Ordinal));
        }

        private static bool StartsWith(string value, string prefix)
        {
            return value.StartsWith(prefix ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class CompletionInput
    {
        public CompletionInput(string argumentsText, int cursorPosition, string wordToComplete)
        {
            ArgumentsText = argumentsText;
            CursorPosition = cursorPosition;
            WordToComplete = wordToComplete ?? string.Empty;
        }

        public string ArgumentsText { get; private set; }
        public int CursorPosition { get; private set; }
        public string WordToComplete { get; private set; }
    }
}
