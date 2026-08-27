using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Extensions;

namespace ComplexTweaks.Tweaks;

internal class AutoQueue : Tweak {
    public override string Name => "Auto Queue";
    public override string Description => "Auto queue into a pre-checked duty (on zone change).\n" +
        "If in a party, waits for all players to be in the overworld, and either targetable or in another zone from you.";

    public override void OnEnable() => IClientState.Get().TerritoryChanged += OnTerritoryChanged;
    public override void OnDisable() => IClientState.Get().TerritoryChanged -= OnTerritoryChanged;

    private void OnTerritoryChanged(uint obj) {
        if (IPlayerState.Get() is { IsInDuty: true } or { IsPenalised: true }) return;
        Automation.Start(AutoTask.From(async t => {
            await t.WaitUntil(() => !IObjectTable.Get().LocalPlayer.IsBusy, "NotBusy");
            await t.WaitUntil(() => IPartyList.Get().All(p => !p.Territory.Value.IsDuty), "WaitForPartyNotInDuty");
            await t.WaitUntil(ICondition.Get().CanQueue, "WaitForQueueCondition", timeout: TimeSpan.FromSeconds(30));
            QueueSelectedDuty();
        }, name: "AutoQueue"));
    }

    private static unsafe void QueueSelectedDuty() {
        var content = AgentContentsFinder.Instance()->SelectedContent;
        if (content.FirstOrNull(x => x.ContentType is ContentsType.Roulette) is { Id: var id }) {
            ContentsFinder.Instance()->QueueInfo.QueueRoulette((byte)id);
        }
        else {
            var ids = content.Select(x => x.Id).ToList();
            var array = stackalloc uint[ids.Count];
            for (var i = 0; i < ids.Count; i++)
                array[i] = ids[i];
            ContentsFinder.Instance()->QueueInfo.QueueDuties(array, ids.Count);
        }
    }
}
