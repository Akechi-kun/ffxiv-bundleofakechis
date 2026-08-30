using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using System.Diagnostics.CodeAnalysis;

namespace ComplexTweaks.Tweaks;

public class AutoFollowConfiguration {
    [IntConfig] public int DistanceToKeep = 3;
    [IntConfig] public int DisableIfFurtherThan;
    [BoolConfig] public bool OnlyInDuty;
    [BoolConfig] public bool ExcludeCombat;
    [StringConfig] public string AutoFollowName = string.Empty;
}

public class AutoFollow : Tweak<AutoFollowConfiguration> {
    public override string Name => "Auto Follow";
    public override string Description
        => "True Auto Follow. Trigger with command while targeting someone. Use it with no target to wipe the current master.\n" +
        "If multiboxing, you can send \"autofollow\" to chat and anyone in the party with this feature enabled will follow.\n" +
        "You can also add a number argument to specify the distance to keep, or add the off argument to clear the current master.";

    private OverrideMovement movement = null!;
    private MasterRef _master;
    private delegate void FlyDelegate(nint gameObject);
    private readonly FlyDelegate Fly = IGameInteropProvider.Get().GetDelegate<FlyDelegate>("E8 ?? ?? ?? ?? 40 84 F6 74 ?? 8D 43"); // 7.41hf1 incase I take three years to get back to this

    [CommandHandler("/autofollow", "Enable AutoFollow")]
    internal void OnCommand(string command, string arguments) {
        if (!arguments.IsEmpty) {
            if (IObjectTable.Get().FirstOrDefault(o => o.Name.TextValue.ToLowerInvariant().Contains(arguments, StringComparison.InvariantCultureIgnoreCase)) is { } obj) {
                _master = MasterRef.FromObject(obj);
                IToastGui.Get().ShowNormal($"Auto following {obj.Name}");
                return;
            }
            else {
                _master = new MasterRef(null, arguments);
                return;
            }
        }
        if (ITargetManager.Get().Target != null)
            SetMaster();
        else
            ClearMaster();
    }

    public override void OnEnable() {
        IFramework.Get().Update += Follow;
        IChatGui.Get().ChatMessage += OnChatMessage;
        movement = new();
    }

    public override void OnDisable() {
        IFramework.Get().Update -= Follow;
        IChatGui.Get().ChatMessage -= OnChatMessage;
        movement.Dispose();
    }

    private void SetMaster() {
        try {
            if (ITargetManager.Get().Target is { Name.TextValue: var name } target) {
                _master = MasterRef.FromObject(target);
                IToastGui.Get().ShowNormal($"Auto following {name}");
            }
            else {
                _master = default;
                IToastGui.Get().ShowNormal("Auto following off");
            }
        }
        catch { }
    }

    private void ClearMaster() {
        _master = default;
        movement.Enabled = false;
        IToastGui.Get().ShowNormal("Auto following off");
    }

    private void Follow(IFramework framework) {
        if (IObjectTable.Get().LocalPlayer is not { } player) return;
        if (!ICondition.Get()[ConditionFlag.InFlight] && Automation.Running) return; // want to abort, not return, if in flight
        if (_master.IsEmpty && Config.AutoFollowName.IsEmpty) return;

        if (!TryGetMaster(out var master)) {
            movement.Enabled = false;
            return;
        }

        if (ShouldStopForConfig(master)) {
            movement.Enabled = false;
            return;
        }

        if (ICondition.Get()[ConditionFlag.InFlight]) {
            Automation.Stop();
        }

        if (ICondition.Get()[ConditionFlag.RidingPillion]) return;

        if (master.ObjectKind == ObjectKind.Pc) {
            if (TrySprint(master)) return;
            if (TryPillion(master)) return;
            if (TryMount(master)) return;
            if (TryFly(master)) return;
            if (TryDismount(master)) return;
        }

        if (player.DistanceTo(master) <= Config.DistanceToKeep) {
            movement.Enabled = false;
            return;
        }

        movement.Enabled = true;
        movement.DesiredPosition = master.Position;
    }

    private bool TryGetMaster([NotNullWhen(true)] out IGameObject? master) {
        master = IObjectTable.Get().FirstOrDefault(x => !_master.IsEmpty && _master.Matches(x) || !Config.AutoFollowName.IsEmpty && x.Name.TextValue.EqualsIgnoreCase(Config.AutoFollowName));
        return master != null;
    }

    private bool ShouldStopForConfig(IGameObject master) {
        if (Config.DisableIfFurtherThan > 0 && master.DistanceTo() >= Config.DisableIfFurtherThan)
            return true;

        if (Config.OnlyInDuty && !IPlayerState.Get().IsInDuty)
            return true;

        if (Config.ExcludeCombat && ICondition.Get()[ConditionFlag.InCombat])
            return true;

        return false;
    }

    private unsafe bool TrySprint(DGameObject master) {
        if (master is IBattleChara { StatusList: var status } && status.Any(s => s.StatusId is 50)) {
            if (MJIManager.Instance()->IsPlayerInSanctuary && (IObjectTable.Get().LocalPlayer?.StatusList.None(s => s.StatusId is 50) ?? false)) {
                return ActionManager.Instance()->UseAction(ActionType.Action, 31314);
            }
            else {
                if (IObjectTable.Get().LocalPlayer?.StatusList.None(s => s.StatusId is 50) ?? false)
                    return ActionManager.Instance()->UseAction(ActionType.GeneralAction, 4);
            }
        }
        return false;
    }

    private bool TryPillion(IGameObject master) {
        if (!IPartyList.Get().Any(p => p.EntityId == master.GameObjectId) || !master.CanRidePillion())
            return false;

        if (master.DistanceTo() > 3) {
            movement.Enabled = true;
            movement.DesiredPosition = master.Position;
            return true;
        }

        movement.Enabled = false;
        if (DismountIfMounted())
            return true;

        if (Automation.Running)
            return true;

        Automation.Start(AutoTask.From(async t => {
            t.Log("Detected mounted party member with extra seats, mounting...");
            GameMain.ExecuteCommand(CommandFlag.RidePillion, (int)master.EntityId, 10);
            await t.WaitUntil(() => ICondition.Get()[ConditionFlag.Mounted], "Mounted", timeout: TimeSpan.FromSeconds(5));
        }, name: "Pillion"));
        return true;
    }

    private unsafe bool TryMount(IGameObject master) {
        if (!master.Character->IsMounted() || !CanMount())
            return false;

        movement.Enabled = false;
        ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
        return true;
    }

    private bool TryFly(IGameObject master) {
        if (!master.IsFlying || !CanFly())
            return false;

        movement.Enabled = false;
        if (Automation.Running)
            return true;

        Automation.Start(AutoTask.From(async t => {
            UseJump();
            await t.DelayMs(50);
            UseJump();
        }, name: "Fly"));

        // TODO: find a way to incorporate this. Need to jump and trigger at the apex or something
        //Fly((nint)Player.GameObject);
        return true;
    }

    private static unsafe bool DismountIfMounted() {
        if (!ICondition.Get()[ConditionFlag.Mounted])
            return false;
        ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
        return true;
    }

    private static unsafe void UseJump() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);

    private unsafe bool TryDismount(IGameObject master) {
        if (master.Character->IsMounted() || !ICondition.Get()[ConditionFlag.Mounted])
            return false;

        movement.Enabled = false;
        ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
        return true;
    }

    private static bool CanMount() => !ICondition.Get()[ConditionFlag.Mounted] && !ICondition.Get()[ConditionFlag.Mounting] && !ICondition.Get()[ConditionFlag.InCombat] && !ICondition.Get()[ConditionFlag.Casting];
    private static bool CanFly() => Control.CanFly && !ICondition.Get()[ConditionFlag.InFlight];

    private readonly record struct MasterRef(uint? Id, string? Name) {
        public bool IsEmpty => Id is null && string.IsNullOrEmpty(Name);

        public static MasterRef FromObject(IGameObject obj)
            => new(obj.EntityId, obj.Name.TextValue);

        public bool Matches(IGameObject obj)
            => Id is not null && obj.EntityId == Id || !string.IsNullOrEmpty(Name) && obj.Name.TextValue.EqualsIgnoreCase(Name);
    }

    private void OnChatMessage(IHandleableChatMessage message) {
        if (message.LogKind != XivChatType.Party) return;
        var player = message.Sender.Payloads.SingleOrDefault(x => x is PlayerPayload) as PlayerPayload;
        if (message.Message.TextValue.ContainsIgnoreCase("autofollow")) {
            if (int.TryParse(message.Message.TextValue.Split("autofollow")[1], out var distance))
                Config.DistanceToKeep = distance;
            else if (message.Message.TextValue.ContainsIgnoreCase("autofollow off"))
                ClearMaster();
            else {
                if (IObjectTable.Get().FirstOrDefault(o => o.Name.TextValue.Equals(player?.PlayerName)) is { } actor) {
                    ITargetManager.Get().Target = actor;
                    SetMaster();
                }
            }
        }
    }
}
