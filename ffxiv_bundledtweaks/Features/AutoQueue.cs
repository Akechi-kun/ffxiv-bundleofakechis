using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace ComplexTweaks.Features;

[Tweak]
internal class AutoQueue : Tweak
{
    public override string Name => "Auto Queue";
    public override string Description => "Auto queue into a pre-checked duty.\nTriggers on zone change.\n If everyone in the party is nearby each other in the overworld (targetable range), it will wait for them to be fully loaded into the overworld first.";

    public override void Enable() => Svc.ClientState.TerritoryChanged += OnTerritoryChanged;
    public override void Disable() => Svc.ClientState.TerritoryChanged -= OnTerritoryChanged;

    private unsafe void OnTerritoryChanged(ushort obj)
    {
        if (Player.IsInDuty || Player.HasPenalty) return;
        TaskManager.Enqueue(() => !IsOccupied());
        TaskManager.Enqueue(() => Svc.Party.All(p => p.Territory.Value.TerritoryIntendedUse.Value.RowId is (byte)TerritoryIntendedUseEnum.City_Area or (byte)TerritoryIntendedUseEnum.Open_World));
        TaskManager.Enqueue(() => !Svc.Party.All(p => p.Territory.Value.RowId == Player.Territory) || Svc.Party.All(p => p.GameObject?.IsTargetable ?? false));
        TaskManager.Enqueue(QueueSelectedDuty);
    }

    private unsafe bool QueueSelectedDuty()
    {
        var ids = AgentContentsFinder.Instance()->SelectedContent.Select(x => x.Id).ToList();
        var array = stackalloc uint[ids.Count];
        for (var i = 0; i < ids.Count; i++)
            array[i] = ids[i];
        if (AgentContentsFinder.Instance()->SelectedContent.Any(x => x.ContentType is ContentsId.ContentsType.Roulette))
        {
            ContentsFinder.Instance()->QueueInfo.QueueRoulette((byte)AgentContentsFinder.Instance()->SelectedContent.First().Id);
            return true;
        }
        else
        {
            ContentsFinder.Instance()->QueueInfo.QueueDuties(array, ids.Count);
            return true;
        }
    }
}
