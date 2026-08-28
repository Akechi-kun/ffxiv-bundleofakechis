using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ComplexTweaks.Tweaks;

public partial class PreventConfirmTargetingChat : Tweak {
    public override string Name => "Prevent Confirm Targeting Chat";
    public override string Description => "If the confirm keybind would target the chat window, prevent it.";

    [AddressHook<AtkModule>(nameof(AtkModule.MemberFunctionPointers.HandleInput))]
    public unsafe byte HandleInput(AtkModule* atkModule, UIInputData* inputData, bool isPadMouseModeEnabled) {
        var ret = HandleInputHook.Original(atkModule, inputData, isPadMouseModeEnabled);
        if (atkModule is null || inputData is null)
            return ret;

        try {
            if (inputData->IsInputIdPressed(InputId.OK) && atkModule->AtkUnitManager is not null and var unitManager && unitManager->FocusedAddon is not null and var focusedAddon && focusedAddon->NameString == "ChatLog") {
                unitManager->FocusedAddon = null;
                if (atkModule->AtkStage is not null)
                    atkModule->AtkStage->ClearFocus();
            }

            return ret;
        }
        catch (Exception ex) {
            Error(ex, "Error clearing chat focus");
            return ret;
        }
    }
}
