using System;
using System.CommandLine;
using System.Linq;
using System.Reflection;

namespace usbrelay
{
    internal static class IntegrationCommand
    {
        public static Command Create()
        {
            var root = new Command("integration", "Install MCP registrations and generate USB Relay skills for AI tools.");
            root.Subcommands.Add(CreateSetup("install", "Register MCP and create the skill; defaults to detected clients."));
            root.Subcommands.Add(CreateSetup("status", "Preview registrations and skills without writing files."));
            var output = new Option<string>("--output") { Required = true, Description = "Skill directory, which must be named usb-relay." };
            var force = new Option<bool>("--force") { Description = "Replace a customized skill with a backup." };
            var preview = new Option<bool>("--dry-run") { Description = "Show changes without writing files." };
            var skill = new Command("skill", "Generate a standalone SKILL.md for an Agent Skills compatible client.") { Options = { output, force, preview } };
            skill.SetAction(parsed => Execute(() =>
            {
                var installer = CreateInstaller();
                installer.Apply(installer.PlanSkill(parsed.GetValue(output), parsed.GetValue(force)), parsed.GetValue(preview));
            }));
            root.Subcommands.Add(skill);
            root.SetAction(parsed => { Console.Error.WriteLine("Usage: usbrelay integration <install|status|skill> [options]"); return 1; });
            return root;
        }

        private static Command CreateSetup(string name, string description)
        {
            var clients = new Option<string[]>("--client") { Description = "codex, claude-code, cursor, vscode, detected (default), or all.", AllowMultipleArgumentsPerToken = true, Arity = ArgumentArity.OneOrMore };
            clients.CompletionSources.Add(IntegrationInstaller.SupportedClients.Concat(new[] { "detected", "all" }).ToArray());
            var scope = new Option<string>("--scope") { Description = "user (default) or project." };
            scope.CompletionSources.Add(new[] { "user", "project" });
            var project = new Option<string>("--project") { Description = "Project directory; required with --scope project." };
            var home = new Option<string>("--home") { Description = "Generate user configuration under this home instead of the current user's home and environment overrides." };
            var noSkill = new Option<bool>("--no-skill") { Description = "Install only the MCP registration." };
            var force = new Option<bool>("--force") { Description = "Replace a conflicting usbrelay entry or customized skill with backups." };
            var preview = new Option<bool>("--dry-run") { Description = "Preview changes without writing files." };
            var command = new Command(name, description) { Options = { clients, scope, project, home, noSkill, force, preview } };
            command.SetAction(parsed => Execute(() =>
            {
                var options = new IntegrationOptions
                {
                    Clients = (parsed.GetValue(clients)?.Length ?? 0) == 0 ? new[] { "detected" } : parsed.GetValue(clients),
                    Scope = parsed.GetValue(scope) ?? "user",
                    ProjectDirectory = parsed.GetValue(project),
                    HomeDirectory = parsed.GetValue(home),
                    IncludeSkill = !parsed.GetValue(noSkill),
                    Force = parsed.GetValue(force)
                };
                var installer = CreateInstaller();
                installer.Apply(installer.Plan(options), name == "status" || parsed.GetValue(preview));
            }));
            return command;
        }

        private static IntegrationInstaller CreateInstaller() => new IntegrationInstaller(Assembly.GetExecutingAssembly().Location, Console.WriteLine);

        private static int Execute(Action action)
        {
            try { action(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine("[integration] Setup failed: " + ex); return 1; }
        }
    }
}
