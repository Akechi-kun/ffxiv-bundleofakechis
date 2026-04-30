using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ComplexTweaks.Tweaks;

[Tweak(debug: true)]
public unsafe partial class InstantReturn : Tweak {
    public override string Name => "Quick Return";
    public override string Description => "Calls the return function directly";

    public override void Enable() => Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", HandleReturn);
    public override void Disable() => Svc.AddonLifecycle.UnregisterListener(HandleReturn);

    [AddressHook<AgentReturn>(nameof(AgentReturn.MemberFunctionPointers.Return))]
    private void AgentReturn_Return(AgentReturn* agent) {
        if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 6) != 0 || Player.IsInPvP)
            AgentReturn_ReturnHook.Original(agent);

        if (InfoProxyCrossRealm.IsLocalPlayerInParty()) {
            if (InfoProxyCrossRealm.IsLocalPlayerPartyLeader())
                InfoProxyPartyMember.Instance()->DisbandParty();
            else
                InfoProxyPartyMember.Instance()->LeaveParty();
        }

        GameMain.ExecuteCommand(CommandFlag.InstantReturn.Value);
    }

    private void HandleReturn(AddonEvent type, AddonArgs args) {
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Return);
        if (agent is null || agent->AddonId != args.Addon.Id) return;

        args.ReceiveEvent(AtkEventType.ButtonClick, 0);
    }
}
