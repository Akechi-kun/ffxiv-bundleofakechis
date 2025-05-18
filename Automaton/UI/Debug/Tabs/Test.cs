using ECommons.GameFunctions;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Automaton.UI.Debug.Tabs;
internal unsafe class TestTab : DebugTab
{
    public override void Draw()
    {
        var x = *((byte*)InfoProxyNoviceNetwork.Instance() + 0x18);
        ImGuiEx.Text($"nn: {x} {x >> 8}");
        if (Svc.Targets.Target is { } target)
        {
            ImGuiEx.Text($"{ActionManager.GetActionInRangeOrLoS(15989, Player.GameObject, target.Struct())}");
            ImGuiEx.Text($"{Player.DistanceTo(target)}:{(*target.Struct()->GetPosition() - *Player.GameObject->GetPosition()).Magnitude}");
            ImGuiEx.Text($"{Vector3.Normalize(target.Position - Player.Position)}{(*target.Struct()->GetPosition() - *Player.GameObject->GetPosition()) / (*target.Struct()->GetPosition() - *Player.GameObject->GetPosition()).Magnitude}");
        }
    }
}
