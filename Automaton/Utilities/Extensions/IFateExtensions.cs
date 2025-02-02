using Dalamud.Game.ClientState.Fates;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using ImGuiNET;

namespace Automaton.Utilities;
public static class IFateExtensions
{
    public static unsafe FateContext* Struct(this IFate fate) => (FateContext*)fate.Address;

    public static void Print(this IFate fate)
    {
        ImGui.TextColored(Colors.Grey3, $"[{fate.FateId}]");
        ImGui.SameLine();
        ImGui.TextColored(Colors.White, $"{fate.Name}");

        ImGui.Indent();

        ImGuiX.FieldAndValue("State", fate.State);
        ImGuiX.FieldAndValue("Position", fate.Position);
        ImGuiX.FieldAndValue("Progress", $"{fate.Progress}%");
        ImGuiX.FieldAndValue("Time Remaining", $"{TimeSpan.FromSeconds(fate.TimeRemaining):mm\\:ss} / {TimeSpan.FromSeconds(fate.Duration):mm\\:ss}");
        ImGuiX.FieldAndValue("Level", $"{fate.Level}-{fate.MaxLevel}");
        ImGuiX.FieldAndValue("Bonus", fate.HasBonus);
        ImGuiX.FieldAndValue("Distance To", Player.DistanceTo(fate.Position));

        ImGui.Unindent();
    }

    public static string Stringify(this IFate fate) => $"[{fate.FateId}] {fate.Position} {fate.Progress}%% {TimeSpan.FromSeconds(fate.TimeRemaining):mm\\:ss} / {TimeSpan.FromSeconds(fate.Duration):mm\\:ss}";
}
