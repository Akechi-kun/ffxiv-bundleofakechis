namespace ComplexTweaks.Tweaks;

[Tweak]
public class AutoPillion : Tweak {
    public override string Name => "Auto Pillion";
    public override string Description => "Automatically hop in to other peoples' mounts when you are near them.";

    public override void Enable() => Svc.Framework.Update += OnUpdate;
    public override void Disable() => Svc.Framework.Update -= OnUpdate;

    private unsafe void OnUpdate(IFramework framework) {
        if (IObjectTable.Get().LocalPlayer is not { Available: true, IsBusy: false, GameObjectId: var playerId } || Svc.Condition[ConditionFlag.Mounted]) {
            if (TaskManager.Tasks.Count > 0)
                TaskManager.Abort();
            return;
        }

        if (Svc.Party.FirstOrDefault(o => o?.EntityId != playerId && o?.GameObject?.CurrentDistance < 3 && o.GameObject.CanRidePillion(), null) is { GameObject: { } target }) {
            TaskManager.Enqueue(() => Debug("Detected mounted party member with extra seats, mounting..."));
            TaskManager.Enqueue(() => target.BattleChara->RidePillion(10));
            TaskManager.Enqueue(() => Svc.Condition[ConditionFlag.Mounted]);
        }
    }
}
