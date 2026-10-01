using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using Tomlyn;
using Tomlyn.Model;

namespace usbrelay
{
    public sealed class IntegrationOptions
    {
        public string[] Clients { get; set; } = new[] { "detected" };
        public string Scope { get; set; } = "user";
        public string ProjectDirectory { get; set; }
        public string HomeDirectory { get; set; }
        public bool IncludeSkill { get; set; } = true;
        public bool Force { get; set; }
    }

    // Planning validates every destination before installation writes any file.
    // This backend is shared by the GUI and CLI; it never opens a relay device.
    public sealed class IntegrationInstaller
    {
        public static readonly string[] SupportedClients = { "codex", "claude-code", "cursor", "vscode" };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private readonly string executable;
        private readonly Action<string> log;

        public IntegrationInstaller(string executable, Action<string> log)
        {
            this.executable = Path.GetFullPath(executable);
            this.log = log ?? (_ => { });
        }

        public IReadOnlyList<IntegrationChange> Plan(IntegrationOptions options)
        {
            if (!File.Exists(executable)) throw new FileNotFoundException("USB Relay executable does not exist.", executable);
            if (options.Scope != "user" && options.Scope != "project") throw new ArgumentException("Scope must be user or project.");
            if (options.Scope == "project" && string.IsNullOrWhiteSpace(options.ProjectDirectory)) throw new ArgumentException("Project scope requires --project <directory>.");
            if (options.Scope == "user" && options.ProjectDirectory != null) throw new ArgumentException("--project requires --scope project.");
            string home = Path.GetFullPath(options.HomeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            string appData = options.HomeDirectory == null ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) : Path.Combine(home, "AppData", "Roaming");
            string codexHome = EnvironmentDirectory("CODEX_HOME", Path.Combine(home, ".codex"), options.HomeDirectory);
            string claudeHome = EnvironmentDirectory("CLAUDE_CONFIG_DIR", Path.Combine(home, ".claude"), options.HomeDirectory);
            string project = options.Scope == "project" ? Path.GetFullPath(options.ProjectDirectory) : null;
            var clients = ResolveClients(options.Clients, home, appData, codexHome, claudeHome);
            var changes = new List<IntegrationChange>();
            foreach (string client in clients)
            {
                string config;
                string skill;
                if (client == "codex")
                {
                    config = Path.Combine(project == null ? codexHome : Path.Combine(project, ".codex"), "config.toml");
                    skill = Path.Combine(project ?? home, ".agents", "skills", "usb-relay");
                }
                else if (client == "claude-code")
                {
                    bool customClaudeHome = options.HomeDirectory == null && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR"));
                    config = project != null ? Path.Combine(project, ".mcp.json") : Path.Combine(customClaudeHome ? claudeHome : home, ".claude.json");
                    skill = Path.Combine(project == null ? claudeHome : Path.Combine(project, ".claude"), "skills", "usb-relay");
                }
                else if (client == "cursor")
                {
                    config = Path.Combine(project ?? home, ".cursor", "mcp.json");
                    skill = Path.Combine(project ?? home, ".agents", "skills", "usb-relay");
                }
                else
                {
                    string portable = options.HomeDirectory == null ? Environment.GetEnvironmentVariable("VSCODE_PORTABLE") : null;
                    config = project != null ? Path.Combine(project, ".vscode", "mcp.json") : Path.Combine(string.IsNullOrWhiteSpace(portable) ? Path.Combine(appData, "Code") : Path.Combine(portable, "user-data"), "User", "mcp.json");
                    skill = project != null ? Path.Combine(project, ".github", "skills", "usb-relay") : Path.Combine(home, ".copilot", "skills", "usb-relay");
                }
                log("[integration] Planning client=" + client + ", scope=" + options.Scope + ", config=" + config);
                byte[] before = ReadBytes(config);
                string content = client == "codex" ? UpdateToml(Decode(before), options.Force) : UpdateJson(Decode(before), client == "vscode" ? "servers" : "mcpServers", client, options.Force);
                AddChange(changes, client + " MCP", config, before, content);
                if (options.IncludeSkill) AddSkill(changes, skill, client, options.Force);
                else log("[integration] Skill generation disabled for " + client);
            }
            return changes;
        }

        public IReadOnlyList<IntegrationChange> PlanSkill(string directory, bool force)
        {
            if (!File.Exists(executable)) throw new FileNotFoundException("USB Relay executable does not exist.", executable);
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A skill output directory is required.");
            if (!string.Equals(Path.GetFileName(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "usb-relay", StringComparison.Ordinal))
                throw new ArgumentException("The skill directory must be named usb-relay so clients can discover the generated skill.");
            var changes = new List<IntegrationChange>();
            AddSkill(changes, Path.GetFullPath(directory), "standalone", force);
            return changes;
        }

        public void Apply(IReadOnlyList<IntegrationChange> changes, bool dryRun)
        {
            foreach (IntegrationChange change in changes)
                log("[integration] " + (change.IsCurrent ? "Current" : dryRun ? "Would write" : "Will write") + " " + change.Label + ": " + change.Path);
            if (dryRun) { log("[integration] Preview complete; no files written."); return; }
            // Check the complete snapshot again before the first mutation.
            foreach (IntegrationChange change in changes) change.CheckUnchanged();
            foreach (IntegrationChange change in changes)
            {
                if (change.IsCurrent) { log("[integration] Skipping unchanged " + change.Path); continue; }
                change.CheckUnchanged();
                Directory.CreateDirectory(Path.GetDirectoryName(change.Path));
                string temp = change.Path + ".usbrelay-" + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllBytes(temp, change.After);
                    if (change.Before == null) File.Move(temp, change.Path);
                    else
                    {
                        string backup = change.Path + ".usbrelay-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N") + ".bak";
                        File.Replace(temp, change.Path, backup);
                        log("[integration] Backup: " + backup);
                    }
                    log("[integration] Saved " + change.Path);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            log("[integration] Setup complete. Restart the selected clients to load the server and skill. Project configurations may require workspace trust.");
        }

        private string[] ResolveClients(string[] requested, string home, string appData, string codexHome, string claudeHome)
        {
            var clients = new HashSet<string>(StringComparer.Ordinal);
            foreach (string item in requested ?? new[] { "detected" })
            {
                string client = item.ToLowerInvariant();
                if (client == "all") clients.UnionWith(SupportedClients);
                else if (client == "detected")
                {
                    foreach (string candidate in SupportedClients)
                    {
                        string directory = candidate == "codex" ? codexHome : candidate == "claude-code" ? claudeHome : candidate == "cursor" ? Path.Combine(home, ".cursor") : Path.Combine(appData, "Code");
                        string command = candidate == "claude-code" ? "claude" : candidate == "vscode" ? "code" : candidate;
                        bool found = Directory.Exists(directory) || CommandExists(command);
                        log("[integration] Detection client=" + candidate + ", found=" + found);
                        if (found) clients.Add(candidate);
                    }
                }
                else if (SupportedClients.Contains(client)) clients.Add(client);
                else throw new ArgumentException("Unsupported client: " + item + ". Use codex, claude-code, cursor, vscode, detected, or all.");
            }
            if (clients.Count == 0) throw new InvalidOperationException("No supported clients detected. Specify --client <name> to generate configuration explicitly.");
            return SupportedClients.Where(clients.Contains).ToArray();
        }

        private string UpdateToml(string text, bool force)
        {
            var model = Toml.ToModel(text);
            object value;
            TomlTable servers;
            if (model.TryGetValue("mcp_servers", out value)) servers = value as TomlTable ?? throw new FormatException("mcp_servers must be a TOML table.");
            else { servers = new TomlTable(); model["mcp_servers"] = servers; }
            TomlTable server;
            if (servers.TryGetValue("usbrelay", out value)) server = value as TomlTable ?? throw new FormatException("mcp_servers.usbrelay must be a TOML table.");
            else { server = new TomlTable(); servers["usbrelay"] = server; }
            server.TryGetValue("command", out value);
            if (server.Count != 0) CheckOwnership(value as string, force);
            var args = server.TryGetValue("args", out value) ? value as TomlArray : null;
            bool current = Equals(server.ContainsKey("command") ? server["command"] : null, executable) && args != null && args.Count == 1 && Equals(args[0], "mcp") && !server.ContainsKey("url") && (!server.ContainsKey("enabled") || Equals(server["enabled"], true));
            if (current) { log("[integration] Codex registration already matches."); return text; }
            server.Remove("url");
            server.Remove("bearer_token_env_var");
            server.Remove("http_headers");
            server.Remove("env_http_headers");
            server["command"] = executable;
            server["args"] = new TomlArray { "mcp" };
            server["enabled"] = true;
            return Toml.FromModel(model);
        }

        private string UpdateJson(string text, string serverKey, string client, bool force)
        {
            var root = string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject;
            if (root == null) throw new FormatException("Client configuration must be a JSON object.");
            JsonObject servers;
            if (root.ContainsKey(serverKey)) servers = root[serverKey] as JsonObject ?? throw new FormatException(serverKey + " must be a JSON object.");
            else { servers = new JsonObject(); root[serverKey] = servers; }
            JsonObject server;
            if (servers.ContainsKey("usbrelay")) server = servers["usbrelay"] as JsonObject ?? throw new FormatException("usbrelay must be a JSON object.");
            else { server = new JsonObject(); servers["usbrelay"] = server; }
            if (server.Count != 0) CheckOwnership(server["command"]?.GetValue<string>(), force);
            var args = server["args"] as JsonArray;
            bool current = server["command"]?.GetValue<string>() == executable && args != null && args.Count == 1 && args[0]?.GetValue<string>() == "mcp" && !server.ContainsKey("url") && server["type"]?.GetValue<string>() == "stdio";
            if (current) { log("[integration] " + client + " registration already matches."); return text; }
            server.Remove("url");
            server.Remove("headers");
            server["command"] = executable;
            server["args"] = new JsonArray("mcp");
            server["type"] = "stdio";
            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        }

        private void CheckOwnership(string command, bool force)
        {
            if (!force && !string.Equals(command, executable, StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(command) || !string.Equals(Path.GetFileName(command), "usbrelay.exe", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("An existing usbrelay MCP entry launches a different command. Use --force to replace it with a backup.");
            log("[integration] Existing USB Relay entry can be updated; force=" + force);
        }

        private void AddSkill(List<IntegrationChange> changes, string directory, string client, bool force)
        {
            string path = Path.Combine(directory, "SKILL.md");
            if (changes.Any(change => string.Equals(change.Path, path, StringComparison.OrdinalIgnoreCase))) return;
            byte[] before = ReadBytes(path);
            string content = GenerateSkill();
            string manifest = Path.Combine(directory, ".usbrelay-sha256");
            byte[] previousHash = ReadBytes(manifest);
            if (before != null && Decode(before) != content && !force && (previousHash == null || Decode(previousHash).Trim() != Hash(before)))
                throw new InvalidOperationException("Skill is customized or belongs to another installer: " + path + ". Use --force to replace it with a backup.");
            log("[integration] Planning " + client + " skill: " + path);
            AddChange(changes, client + " skill", path, before, content);
            AddChange(changes, "skill checksum", manifest, previousHash, Hash(Utf8.GetBytes(content)) + "\n");
        }

        public string GenerateSkill()
        {
            var content = new StringBuilder();
            content.AppendLine("---");
            content.AppendLine("name: usb-relay");
            content.AppendLine("description: Discover and control local USB relay boards, name devices and channels, and create or run USB Relay sequences. Use when the user asks to switch relay channels, control connected equipment, automate power cycles, or manage USB Relay scripts using MCP or the usbrelay CLI.");
            content.AppendLine("---");
            content.AppendLine();
            content.AppendLine("<!-- Generated by usbrelay integration. -->");
            content.AppendLine("# USB Relay");
            content.AppendLine();
            content.AppendLine("Use the `usbrelay` MCP server when available. Clients may prefix its tool names. This skill does not grant permission beyond the user's request.");
            content.AppendLine();
            content.AppendLine("1. Call `relay_list` before choosing a board. Read its device paths, friendly names, channel names and current states. Never infer channel identity from list order.");
            content.AppendLine("2. Select the exact board and channel the user requested. Serial numbers can be duplicated; use the full device path or a unique friendly name when ambiguous. Ask for the missing target if the request does not identify it.");
            content.AppendLine("3. Use `relay_set_channel` for an explicit ON/OFF change. Read back with `relay_list` and report the observed state. Call `relay_all_off` only when turning off all connected boards is requested; do not add it as cleanup.");
            content.AppendLine("4. Before writing a sequence, call `sequence_functions` for the current DSL and `sequence_list` to avoid overwriting existing work. Read existing scripts with `sequence_read`, inspect their external commands and relay actions, then use `sequence_add` or `sequence_modify` for the requested changes.");
            content.AppendLine("5. Run a sequence only within the user's authorized task. Review its script and `sequence_status` first. The `sequence_run` confirm parameter answers every Confirm in that run: set it explicitly according to the user's authorization. With confirm=false the script still runs and may switch relays on other branches; it is not a dry run. Cancellation stops later actions and does not undo channels already switched.");
            content.AppendLine("6. Treat MCP isError responses and failed CLI exit codes as failures. Re-list devices after connection errors; do not retry an uncertain hardware write blindly. Share the actionable error and any state verified afterward.");
            content.AppendLine();
            content.AppendLine("## Available MCP tools");
            content.AppendLine();
            foreach (MethodInfo method in typeof(UsbRelayMcpTools).GetMethods().OrderBy(method => method.Name))
            {
                var tool = method.GetCustomAttribute<McpServerToolAttribute>();
                if (tool != null) content.AppendLine("- `" + tool.Name + "`: " + method.GetCustomAttribute<DescriptionAttribute>()?.Description);
            }
            content.AppendLine();
            content.AppendLine("## CLI fallback");
            content.AppendLine();
            content.AppendLine("Executable: `" + executable.Replace("`", "\\`") + "`. In PowerShell invoke the quoted executable with `&`; wait for completion and capture stdout, stderr and the exit code when using a process API.");
            content.AppendLine("Use `--help` for current syntax, `--list` / `--status` to discover boards, and `--device-path <path> --on <channel>` or `--off <channel>` to switch a specific channel. Name selectors are `--device-name`, `--on-name`, and `--off-name`.");
            content.AppendLine("Use `sequence <query|read|add|modify|remove|status|run|functions>` to manage scripts. `--script-file` accepts a script file when adding or modifying. CLI `sequence run` answers confirmations affirmatively and can execute external commands. Review the script and authorization before running it.");
            content.AppendLine("GUI, CLI and MCP share per-user names and sequences. Avoid overlapping hardware operations across processes; locks do not span separate processes. Refresh the GUI after edits through another interface.");
            return content.ToString();
        }

        private static string EnvironmentDirectory(string variable, string fallback, string explicitHome)
        {
            string value = explicitHome == null ? Environment.GetEnvironmentVariable(variable) : null;
            return Path.GetFullPath(string.IsNullOrWhiteSpace(value) ? fallback : value);
        }

        private static bool CommandExists(string command)
        {
            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                foreach (string extension in new[] { ".exe", ".cmd", ".bat", ".ps1" })
                    if (!string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory.Trim('"'), command + extension))) return true;
            return false;
        }

        private static void AddChange(List<IntegrationChange> changes, string label, string path, byte[] before, string after)
            => changes.Add(new IntegrationChange(label, path, before, before != null && Decode(before) == after ? before : Utf8.GetBytes(after)));

        internal static byte[] ReadBytes(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        private static string Decode(byte[] bytes) => bytes == null ? "" : new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }

    public sealed class IntegrationChange
    {
        public string Label { get; }
        public string Path { get; }
        internal byte[] Before { get; }
        internal byte[] After { get; }
        public bool IsCurrent => Before != null && Before.SequenceEqual(After);
        internal IntegrationChange(string label, string path, byte[] before, byte[] after)
        {
            Label = label;
            Path = path;
            Before = before;
            After = after;
        }
        internal void CheckUnchanged()
        {
            byte[] actual = IntegrationInstaller.ReadBytes(Path);
            if ((Before == null) != (actual == null) || (Before != null && !Before.SequenceEqual(actual)))
                throw new IOException("Configuration changed during setup; run preview again: " + Path);
        }
    }
}
