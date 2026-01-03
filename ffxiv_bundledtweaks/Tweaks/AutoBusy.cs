using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace ComplexTweaks.Tweaks;

[Tweak]
public unsafe partial class AutoBusy : Tweak {
    public override string Name => "Auto Busy";
    public override string Description => "Toggles busy while you're teleporting.";

    private bool _teleportCast;

    [AddressHook<ActionManager>(nameof(ActionManager.MemberFunctionPointers.UseAction))]
    private bool UseAction(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted) {
        if (actionType is ActionType.Action && actionId is 5 && Player.OnlineStatus.RowId is not 12) {
            Log($"Casting teleport. Busy status on");
            _teleportCast = true;
            InfoProxyDetail.Instance()->SendOnlineStatusUpdate(12);
        }
        return UseActionHook.Original(self, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
    }

    //[AddressHook<ActionEffectHandler>(nameof(ActionEffectHandler.MemberFunctionPointers.Receive))]
    //private void ProcessPacketActionEffect(uint casterID, Character* casterObj, Vector3* targetPos, ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targets) {
    //    if ((ActionType)header->ActionType is ActionType.Action && header->ActionId is 5 && Player.OnlineStatus.RowId is 12) {
    //        InfoProxyDetail.Instance()->RefreshOnlineStatus();
    //    }
    //    ProcessPacketActionEffectHook.Original(casterID, casterObj, targetPos, header, effects, targets);
    //}

    //[SigHook("E8 ?? ?? ?? ?? 41 0F B6 56 ?? 44 0F 28 8C 24 ?? ?? ?? ??")]
    //private void* Character_CompleteCast(GameObject* thisPtr, ActionType actionType, uint actionId, int a4, GameObjectId objectId, float* a6, float value, ushort a8, int a9, uint entityId) {
    //    if (thisPtr->GetGameObjectId() == Player.GameObject->GetGameObjectId() && actionType is ActionType.Action && actionId is 5 && Player.OnlineStatus.RowId is 12) {
    //        Log($"Teleport cast finished. Busy status off");
    //        InfoProxyDetail.Instance()->RefreshOnlineStatus();
    //    }
    //    return Character_CompleteCastHook.Original(thisPtr, actionType, actionId, a4, objectId, a6, value, a8, a9, entityId);
    //}

    [SigHook("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64")]
    internal void ProcessPacketActorControl(uint actorID, uint category, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, ulong targetID, byte replaying) {
        if (actorID == Player.Object?.EntityId && category is 263 && _teleportCast && Player.OnlineStatus.RowId is 12) {
            Log($"Teleport cast finished. Busy status off");
            InfoProxyDetail.Instance()->RefreshOnlineStatus();
        }
        ProcessPacketActorControlHook.Original(actorID, category, p1, p2, p3, p4, p5, p6, p7, p8, targetID, replaying);
    }
}
