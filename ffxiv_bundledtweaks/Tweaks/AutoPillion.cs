using FFXIVClientStructs.FFXIV.Client.Game;

namespace ComplexTweaks.Tweaks;

public class AutoPillion : Tweak {
    public override string Name => "Auto Pillion";
    public override string Description => "Automatically hop in to other peoples' mounts when you are near them.";

    public override void OnEnable() => IFramework.Get().Update += OnUpdate;
    public override void OnDisable() => IFramework.Get().Update -= OnUpdate;

    private void OnUpdate(IFramework framework) {
        if (IObjectTable.Get().LocalPlayer is not { Available: true, IsBusy: false, GameObjectId: var playerId } || ICondition.Get()[ConditionFlag.Mounted]) {
            if (Automation.Running)
                Automation.Stop();
            return;
        }

        if (Automation.Running)
            return;

        if (IPartyList.Get().FirstOrDefault(o => o?.EntityId != playerId && o?.GameObject?.CurrentDistance < 3 && o.GameObject.CanRidePillion(), null) is { GameObject.EntityId: var entityId }) {
            Automation.Start(AutoTask.From(async t => {
                t.Log("Detected mounted party member with extra seats, mounting...");
                GameMain.ExecuteCommand(CommandFlag.RidePillion, (int)entityId, 10);
                await t.WaitUntil(() => ICondition.Get()[ConditionFlag.Mounted], "Mounted", timeout: TimeSpan.FromSeconds(5));
            }, name: "AutoPillion"));
        }
    }
}
