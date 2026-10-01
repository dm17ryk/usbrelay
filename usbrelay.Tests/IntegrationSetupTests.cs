using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Tomlyn;
using Tomlyn.Model;
using usbrelay;

namespace usbrelay.Tests
{
    internal static class IntegrationSetupTests
    {
        public static void Run()
        {
            foreach (Action test in new Action[] { UserInstallPreservesSettingsAndIsRepeatable, ProjectInstallUsesClientScopes, InvalidConfigPreventsEveryWrite, ConflictsRequireForceAndAreBackedUp, CustomizedSkillsArePreserved, ConcurrentEditIsDetected, EnvironmentOverridesAreRespected, PackagedCliGeneratesStandaloneSkills })
            {
                test();
                Console.WriteLine("PASS " + test.Method.Name);
            }
        }

        private static void UserInstallPreservesSettingsAndIsRepeatable()
        {
            string home = TempDirectory();
            string codex = Path.Combine(home, ".codex", "config.toml");
            Write(codex, "# keep this comment\nmodel = \"test-model\"\n[mcp_servers.other]\ncommand = \"other.exe\"\nargs = [\"--flag\"]\n[profiles.dev]\nmodel = \"another-model\"\n");
            string claude = Path.Combine(home, ".claude.json");
            Write(claude, "{\"permissions\":{\"keep\":true},\"mcpServers\":{\"other\":{\"command\":\"keep.exe\"}},\"projects\":{\"existing\":{\"trusted\":true}}}");
            string vscode = Path.Combine(home, "AppData", "Roaming", "Code", "User", "mcp.json");
            Write(vscode, "{ // comment\n\"inputs\":[],\"servers\":{\"other\":{\"command\":\"other.exe\"},},}");
            var installer = Installer();
            var options = new IntegrationOptions { Clients = new[] { "all" }, HomeDirectory = home };
            var plan = installer.Plan(options);
            installer.Apply(plan, true);
            Check(!File.Exists(Path.Combine(home, ".cursor", "mcp.json")), "Preview wrote configuration");
            Check(!Directory.Exists(Path.Combine(home, ".agents")), "Preview created skill directories");
            installer.Apply(plan, false);
            var model = Toml.ToModel(File.ReadAllText(codex));
            Check((string)model["model"] == "test-model" && File.ReadAllText(codex).Contains("# keep this comment"), "Codex settings or comment lost");
            var servers = (TomlTable)model["mcp_servers"];
            Check(((TomlTable)servers["other"])["command"].Equals("other.exe"), "Unrelated Codex MCP lost");
            Check(((TomlTable)servers["usbrelay"])["command"].Equals(Executable), "Codex executable not registered");
            var json = JsonNode.Parse(File.ReadAllText(claude));
            Check(json["permissions"]["keep"].GetValue<bool>() && json["projects"]["existing"]["trusted"].GetValue<bool>(), "Claude settings lost");
            Check(json["mcpServers"]["other"]["command"].GetValue<string>() == "keep.exe", "Other Claude MCP lost");
            Check(JsonNode.Parse(File.ReadAllText(vscode))["servers"]["usbrelay"]["type"].GetValue<string>() == "stdio", "VS Code server schema incorrect");
            Check(JsonNode.Parse(File.ReadAllText(Path.Combine(home, ".cursor", "mcp.json")))["mcpServers"]["usbrelay"]["type"].GetValue<string>() == "stdio", "Cursor server must explicitly declare stdio");
            Check(File.Exists(Path.Combine(home, ".agents", "skills", "usb-relay", "SKILL.md")), "Codex/Cursor skill missing");
            Check(File.Exists(Path.Combine(home, ".claude", "skills", "usb-relay", "SKILL.md")), "Claude skill missing");
            Check(File.Exists(Path.Combine(home, ".copilot", "skills", "usb-relay", "SKILL.md")), "VS Code skill missing");
            Check(plan.Count(change => change.Path.EndsWith("SKILL.md")) == 3, "Shared skill was duplicated");
            int backups = Directory.GetFiles(home, "*.bak", SearchOption.AllDirectories).Length;
            var repeat = installer.Plan(options);
            Check(repeat.All(change => change.IsCurrent), "Repeated setup is not idempotent");
            installer.Apply(repeat, false);
            Check(Directory.GetFiles(home, "*.bak", SearchOption.AllDirectories).Length == backups, "Repeated setup made needless backups");
        }

        private static void ProjectInstallUsesClientScopes()
        {
            string project = TempDirectory();
            string home = TempDirectory();
            var installer = Installer();
            installer.Apply(installer.Plan(new IntegrationOptions { Clients = new[] { "all" }, Scope = "project", ProjectDirectory = project, HomeDirectory = home }), false);
            Check(File.Exists(Path.Combine(project, ".codex", "config.toml")), "Codex project config missing");
            Check(File.Exists(Path.Combine(project, ".mcp.json")), "Claude project config missing");
            Check(File.Exists(Path.Combine(project, ".cursor", "mcp.json")), "Cursor project config missing");
            Check(File.Exists(Path.Combine(project, ".vscode", "mcp.json")), "VS Code project config missing");
            Check(File.Exists(Path.Combine(project, ".github", "skills", "usb-relay", "SKILL.md")), "VS Code project skill missing");
            Check(Directory.GetFiles(home, "*", SearchOption.AllDirectories).Length == 0, "Project install changed user scope");
            ExpectFailure(() => installer.Plan(new IntegrationOptions { Scope = "project" }));
        }

        private static void InvalidConfigPreventsEveryWrite()
        {
            string home = TempDirectory();
            Write(Path.Combine(home, ".cursor", "mcp.json"), "{\"mcpServers\": []}");
            ExpectFailure(() => Installer().Plan(new IntegrationOptions { Clients = new[] { "codex", "cursor" }, HomeDirectory = home }));
            Check(!Directory.Exists(Path.Combine(home, ".codex")), "Failed plan wrote another client's files");
            Write(Path.Combine(home, ".codex", "config.toml"), "model = [ invalid");
            ExpectFailure(() => Installer().Plan(new IntegrationOptions { Clients = new[] { "codex" }, HomeDirectory = home }));
        }

        private static void ConflictsRequireForceAndAreBackedUp()
        {
            string home = TempDirectory();
            string path = Path.Combine(home, ".claude.json");
            string original = "{\"keep\":42,\"mcpServers\":{\"usbrelay\":{\"type\":\"http\",\"url\":\"https://example.test\"}}}";
            Write(path, original);
            var options = new IntegrationOptions { Clients = new[] { "claude-code" }, HomeDirectory = home, IncludeSkill = false };
            ExpectFailure(() => Installer().Plan(options));
            Check(File.ReadAllText(path) == original, "Collision changed existing registration");
            options.Force = true;
            var installer = Installer();
            installer.Apply(installer.Plan(options), false);
            var json = JsonNode.Parse(File.ReadAllText(path));
            Check(json["keep"].GetValue<int>() == 42 && json["mcpServers"]["usbrelay"]["url"] == null, "Forced replacement lost settings or retained HTTP URL");
            Check(File.ReadAllText(Directory.GetFiles(home, "*.bak").Single()) == original, "Original config not backed up exactly");
        }

        private static void CustomizedSkillsArePreserved()
        {
            string directory = Path.Combine(TempDirectory(), "usb-relay");
            var installer = Installer();
            installer.Apply(installer.PlanSkill(directory, false), false);
            string path = Path.Combine(directory, "SKILL.md");
            File.AppendAllText(path, "\nMy custom instructions\n");
            ExpectFailure(() => installer.PlanSkill(directory, false));
            installer.Apply(installer.PlanSkill(directory, true), false);
            Check(Directory.GetFiles(directory, "SKILL.md*.bak").Length == 1, "Customized skill not backed up");
            Check(!File.ReadAllText(path).Contains("My custom instructions"), "Force did not regenerate skill");
        }

        private static void ConcurrentEditIsDetected()
        {
            string home = TempDirectory();
            var installer = Installer();
            var plan = installer.Plan(new IntegrationOptions { Clients = new[] { "codex" }, HomeDirectory = home });
            string path = Path.Combine(home, ".codex", "config.toml");
            Write(path, "model = \"concurrent\"\n");
            ExpectFailure(() => installer.Apply(plan, false));
            Check(File.ReadAllText(path).Contains("concurrent") && !Directory.Exists(Path.Combine(home, ".agents")), "Concurrent config change was overwritten");
        }

        private static void EnvironmentOverridesAreRespected()
        {
            string codex = TempDirectory();
            string claude = TempDirectory();
            string previousCodex = Environment.GetEnvironmentVariable("CODEX_HOME");
            string previousClaude = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            try
            {
                Environment.SetEnvironmentVariable("CODEX_HOME", codex);
                Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", claude);
                var plan = Installer().Plan(new IntegrationOptions { Clients = new[] { "codex", "claude-code" }, IncludeSkill = false });
                Check(plan[0].Path == Path.Combine(codex, "config.toml"), "CODEX_HOME ignored");
                Check(plan[1].Path == Path.Combine(claude, ".claude.json"), "CLAUDE_CONFIG_DIR ignored");
            }
            finally
            {
                Environment.SetEnvironmentVariable("CODEX_HOME", previousCodex);
                Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", previousClaude);
            }
        }

        private static void PackagedCliGeneratesStandaloneSkills()
        {
            string home = TempDirectory();
            string copiedExe = Path.Combine(home, "usbrelay.exe");
            File.Copy(Executable, copiedExe);
            File.Copy(Executable + ".config", copiedExe + ".config");
            string destination = Path.Combine(home, "skill output Ω", "usb-relay");
            RunCli(copiedExe, "integration skill --output \"" + destination + "\"");
            string skill = File.ReadAllText(Path.Combine(destination, "SKILL.md"));
            Check(skill.Contains("name: usb-relay") && skill.Contains("relay_set_channel") && skill.Contains("sequence_functions") && skill.Contains(copiedExe), "Generated skill lacks executable or tools");
            string configHome = Path.Combine(home, "config home");
            RunCli(copiedExe, "integration install --client codex claude-code --home \"" + configHome + "\" --no-skill");
            Check(File.Exists(Path.Combine(configHome, ".codex", "config.toml")) && File.Exists(Path.Combine(configHome, ".claude.json")), "CLI did not install both MCP registrations");
            int originalFiles = Directory.GetFiles(configHome, "*", SearchOption.AllDirectories).Length;
            RunCli(copiedExe, "integration status --home \"" + configHome + "\"");
            Check(Directory.GetFiles(configHome, "*", SearchOption.AllDirectories).Length == originalFiles, "Default detected-client status wrote files");
        }

        private static void RunCli(string executable, string arguments)
        {
            using (var process = Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(20000)) { process.Kill(); process.WaitForExit(5000); throw new InvalidOperationException("CLI integration command timed out: " + arguments); }
                Check(process.ExitCode == 0, "Standalone CLI failed: " + stderr.GetAwaiter().GetResult() + stdout.GetAwaiter().GetResult());
            }
        }

        private static string Executable => typeof(IntegrationInstaller).Assembly.Location;
        private static IntegrationInstaller Installer() => new IntegrationInstaller(Executable, _ => { });
        private static string TempDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "usbrelay-integration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
        private static void Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void ExpectFailure(Action action)
        {
            try { action(); }
            catch (Exception) { return; }
            throw new InvalidOperationException("Expected setup to reject this input.");
        }
    }
}
