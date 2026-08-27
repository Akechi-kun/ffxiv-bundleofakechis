using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ECommons.Events;

namespace ComplexTweaks.Tweaks;

public class EnhancedLoginLogoutConfig {
    public List<EnhancedLoginLogout.CharacterCommands> Chars = [];
    public bool RunCommandsWhenARIsActive = false;
}

public class EnhancedLoginLogout : Tweak<EnhancedLoginLogoutConfig> {
    // TODO: hook logout and run commands then too
    public override string Name => "Enhanced Login";
    public override string Description => "Additional options when logging in.";

    public class CharacterCommands {
        public ulong CID;
        public string Name = string.Empty;
        public List<string> LoginCommands = [];
        //public List<string> LogoutCommands = [];
    }

    public override void DrawConfig() {
        base.DrawConfig();

        ImGui.DrawSection("Login Commands");

        if (AutoRetainerIPC.Get().IsLoaded)
            ImGui.Checkbox("Run Commands if AutoRetainer is active", ref Config.RunCommandsWhenARIsActive);

        if (Config.Chars.All(c => c.CID != 0)) {
            Config.Chars.Add(new CharacterCommands {
                CID = 0,
                Name = "Global",
            });
        }
        if (Config.Chars.All(c => c.CID != IPlayerState.Get().ContentId) && !IPlayerState.Get().CharacterName.IsEmpty) // there's a delay after getting a cid before you have a name
        {
            Config.Chars.Add(new CharacterCommands {
                CID = IPlayerState.Get().ContentId,
                Name = IPlayerState.Get().CharacterName,
            });
        }
        Config.Chars.RemoveAll(c => c.LoginCommands.Count == 0 && c.CID != 0 && c.CID != IPlayerState.Get().ContentId);

        foreach (var c in Config.Chars.OrderByDescending(x => x.Name == "Global")) {
            ImGui.DrawSection(c.Name, drawSeparator: false);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) Config.Chars.Remove(c);

            foreach (var cmd in c.LoginCommands.ToList()) {
                var tmp = cmd;
                if (ImGui.InputText($"##{c.CID}_{cmd}", ref tmp, 150))
                    c.LoginCommands[c.LoginCommands.IndexOf(cmd)] = ConvertToCommand(tmp);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton($"{c.CID}_{cmd}", FontAwesomeIcon.Trash))
                    c.LoginCommands.Remove(cmd);
            }
            var newcmd = string.Empty;
            if (ImGui.InputText($"##{c.CID}_new", ref newcmd, 150, ImGuiInputTextFlags.EnterReturnsTrue))
                c.LoginCommands.Add(ConvertToCommand(newcmd));
        }
    }

    public override void OnEnable() => ProperOnLogin.RegisterInteractable(RunCommands); // TODO: see if regular login can be used yet
    public override void OnDisable() => ProperOnLogin.Unregister(RunCommands);

    private string ConvertToCommand(string cmd) => cmd.StartsWith('/') ? cmd : $"/{cmd}";
    private void RunCommands() {
        if (AutoRetainerIPC.Get().IsLoaded && !Config.RunCommandsWhenARIsActive && (AutoRetainerIPC.Get().IsBusy() || AutoRetainerIPC.Get().GetMultiModeEnabled())) return;
        var commands = Config.Chars
            .Where(x => x.CID == 0 || x.CID == IPlayerState.Get().ContentId)
            .OrderByDescending(x => x.Name == "Global")
            .SelectMany(chr => chr.LoginCommands.Where(c => c.Length >= 3))
            .ToList();
        if (commands.Count == 0)
            return;

        Automation.Start(AutoTask.From(async t => {
            foreach (var cmd in commands) {
                await t.DelayMs(250);
                IChatGui.Get().SendMessage(cmd);
            }
        }, name: "LoginCommands"));
    }
}
