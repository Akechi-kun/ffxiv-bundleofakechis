using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

namespace ComplexTweaks.UI.Debug.Tabs;

internal unsafe class FlagTab : DebugTab {
    public override void Draw() {
        ImGui.Text($"IsFlagMarkerSet: {AgentMap.Instance()->FlagMarkerCount > 0}");
        if (!(AgentMap.Instance()->FlagMarkerCount > 0)) return;

        ImGui.Text($"Territory: {IPlayerState.Get().MapFlag.TerritoryId} {TerritoryType.GetRowOrNull(IPlayerState.Get().MapFlag.TerritoryId)?.Name}");
        var row = Map.GetRowOrNull(IPlayerState.Get().MapFlag.MapId);
        if (row is { } map)
            ImGui.Text($"[{map.RowId}] Size: {map.SizeFactor}, Offset: {map.OffsetX}, {map.OffsetY} Territory: {map.TerritoryType.Value.Name}");

        ImGui.Text($"Map Position: {IPlayerState.Get().MapFlag.Position}");

        if (Service.Navmesh.FlagToPoint() is not { } pos) return;
        ImGui.Text($"World Position: {pos}");

        var territory = IPlayerState.Get().MapFlag.TerritoryId;
        var closest = Coords.FindClosestAetheryte(territory, pos);
        var aetherytes = Aetheryte.Where(x => x.Territory.RowId == territory).OrderBy(a => (pos - Coords.AetherytePosition(a)).LengthSquared());

        foreach (var aetheryte in aetherytes) {
            ImGui.Text($"[{aetheryte.RowId}]");
            ImGui.Indent();
            ImGui.Text($"PlaceName: {aetheryte.PlaceName.Value.Name}");
            ImGui.Text($"AethernetName: {aetheryte.AethernetName.Value.Name}");
            ImGui.Text($"Position: {Coords.AetherytePosition(aetheryte)}");
            ImGui.Text($"Dist: {(pos - Coords.AetherytePosition(aetheryte)).LengthSquared()}");
            ImGui.Unindent();
        }
    }
}
