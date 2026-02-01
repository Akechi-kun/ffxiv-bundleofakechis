using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using System.Linq;

namespace ComplexTweaks.Tweaks;

[Tweak]
public unsafe class AetherCurrents : Tweak {
    public override string Name => "Aether Current Scanner";
    public override string Description => "Lists unattuned aether currents in the current zone and pathfinds to them.";

    public override void DrawConfig() {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        var territory = Svc.ClientState.TerritoryType;
        var playerState = PlayerState.Instance();

        ImGui.TextUnformatted("Unattuned Aether Currents");
        ImGui.Separator();

        var currents = Svc.Objects
            .Where(o => o.ObjectKind == ObjectKind.EventObj)
            .ToList();

        if (currents.Count == 0) {
            ImGui.TextDisabled("All aether currents attuned.");
            return;
        }

        else foreach (var obj in currents) {
                ImGui.Text($"Aether Current {obj.BaseId}");
                ImGui.SameLine();
                if (ImGui.Button($"Go To##{obj.GameObjectId}"))
                    GoTo(obj);
            }
    }

    private void GoTo(IGameObject obj) {
        if (!Service.Navmesh.IsRunning())
            Service.Navmesh.PathfindAndMoveTo(obj.Position, false);
    }
}
