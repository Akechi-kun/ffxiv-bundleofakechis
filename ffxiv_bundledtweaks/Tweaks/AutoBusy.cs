using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace ComplexTweaks.Tweaks;

[Tweak]
public unsafe partial class AutoBusy : Tweak {
    public override string Name => "Auto Busy";
    public override string Description => "Toggles busy while you're teleporting.";

    [AddressHook<ActionManager>(nameof(ActionManager.MemberFunctionPointers.UseAction))]
    private bool UseAction(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted) {
        if (actionType is ActionType.Action && actionId is 5 && Player.OnlineStatus.RowId is not 12) {
            Log($"Casting teleport. Busy status on");
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

    [SigHook("E8 ?? ?? ?? ?? 41 0F B6 56 ?? 44 0F 28 8C 24 ?? ?? ?? ??")]
    private void* Character_CompleteCast(GameObject* thisPtr, ActionType actionType, uint actionId, int a4, GameObjectId objectId, float* a6, float value, ushort a8, int a9, uint entityId) {
        if (thisPtr->GetGameObjectId() == Player.GameObject->GetGameObjectId() && actionType is ActionType.Action && actionId is 5 && Player.OnlineStatus.RowId is 12) {
            Log($"Teleport cast finished. Busy status off");
            InfoProxyDetail.Instance()->RefreshOnlineStatus();
        }
        return Character_CompleteCastHook.Original(thisPtr, actionType, actionId, a4, objectId, a6, value, a8, a9, entityId);
    }
}
