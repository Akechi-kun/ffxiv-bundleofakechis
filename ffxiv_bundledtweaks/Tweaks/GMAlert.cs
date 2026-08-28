using clib.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using System.Threading.Tasks;

namespace ComplexTweaks.Tweaks;

public class GMAlertConfiguration {
    [BoolConfig] public bool Toast = false;
    [BoolConfig] public bool ChatMessage = false;
    [BoolConfig] public bool Sound = false;
    [IntConfig(DependsOn = "Sound", DefaultValue = 3)] public int BeepCount = 3;
    [IntConfig(DependsOn = "Sound", DefaultValue = 250)] public int BeepDuration = 250;
    [IntConfig(DependsOn = "Sound", DefaultValue = 900)] public int BeepFrequency = 900;
    [BoolConfig] public bool KillGame = false;
    public HashSet<string> Commands = [];
}

public partial class GMAlert : Tweak<GMAlertConfiguration> {
    public override string Name => "GM Alert";
    public override string Description => "Various alerts for when a GM is nearby.";

    private string _cmd = string.Empty;
    public override void DrawConfig() {
        ImGui.DrawSection("Upon GM Appearance");

        ImGui.Checkbox("Send Toast Alert", ref Config.Toast);
        ImGui.Checkbox("Send Chat Alert", ref Config.ChatMessage);
        ImGui.Checkbox("Send Sound Alert", ref Config.Sound);
        if (Config.Sound) {
            ImGui.SameLine();
            if (ImGui.IconButton(FontAwesomeIcon.Music, "##SoundPreview", "Preview Beeps"))
                for (var i = 0; i < Config.BeepCount; i++)
                    Task.Run(() => Console.Beep(Config.BeepFrequency, Config.BeepDuration));

            ImGui.Indent();
            ImGui.SliderInt("Beep Count", ref Config.BeepCount, 1, 100);
            ImGui.SameLine();
            ImGui.ResetButton(ref Config.BeepCount, 3);

            ImGui.SliderInt("Beep Duration", ref Config.BeepDuration, 1, 1000);
            ImGui.SameLine();
            ImGui.ResetButton(ref Config.BeepDuration, 250);

            ImGui.SliderInt("Beep Frequency", ref Config.BeepFrequency, 100, 5000);
            ImGui.SameLine();
            ImGui.ResetButton(ref Config.BeepFrequency, 900);
            ImGui.Unindent();
        }

        ImGui.Checkbox("Kill Game", ref Config.KillGame);

        ImGui.TextColoredWrapped(Color.Gold, "Execute Commands");
        if (ImGui.InputText($"##Commands", ref _cmd, 50, ImGuiInputTextFlags.EnterReturnsTrue))
            Config.Commands.Add(_cmd.StartsWith('/') ? _cmd : $"/{_cmd}");

        foreach (var cmd in Config.Commands) {
            ImGui.TextV(cmd);
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(cmd, FontAwesomeIcon.Trash))
                Config.Commands.Remove(cmd);
        }
    }

    [SigHook("48 89 5C 24 ?? 57 48 83 EC 20 0F B6 42 1B")]
    internal unsafe Character* CharacterSetupContainer_InitPlayer(CharacterSetupContainer* thisPtr, SpawnPlayerPacket* packet) {
        var res = CharacterSetupContainer_InitPlayerHook.Original(thisPtr, packet);
        var player = thisPtr->OwnerObject;
        if (player == null || Control.GetLocalPlayer() == player) return res;

        if (packet->GMRank != 0 || player->CharacterData.OnlineStatus is >= 1 and <= 3) {
            if (Config.Toast)
                IToastGui.Get().ShowNormal($"GM {player->NameString} is nearby!");
            if (Config.ChatMessage)
                ModuleMessage($"GM {player->NameString} is nearby!");
            if (Config.Sound)
                for (var i = 0; i < Config.BeepCount; i++)
                    Task.Run(() => Console.Beep(Config.BeepFrequency, Config.BeepDuration));

            if (Config.Commands.Count > 0)
                foreach (var cmd in Config.Commands)
                    IChatGui.Get().ExecuteCommand(cmd);
            if (Config.KillGame)
                IChatGui.Get().ExecuteCommand("/xlkill");
        }

        return res;
    }
}
