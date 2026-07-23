using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Automaton.Tweaks;

[Tweak]
public partial class PreventConfirmTargetingChat : Tweak {
    public override string Name => "Prevent Confirm Targeting Chat";
    public override string Description => "If the confirm keybind would target the chat window, prevent it.";

    [AddressHook<AtkModule>(nameof(AtkModule.MemberFunctionPointers.HandleInput))]
    public unsafe byte HandleInput(AtkModule* atkModule, UIInputData* inputData, bool isPadMouseModeEnabled) {
        try {
            if (inputData->IsInputIdPressed(FFXIVClientStructs.FFXIV.Client.System.Input.InputId.OK)) {
                if (atkModule->AtkUnitManager->FocusedAddon is not null and var f && f->NameString == "ChatLog") {
                    atkModule->AtkUnitManager->FocusedAddon = null;
                    atkModule->AtkStage->ClearFocus();
                }
            }

            return HandleInputHook.Original(atkModule, inputData, isPadMouseModeEnabled);
        }
        catch (Exception ex) {
            Error(ex, $"Error setting focused to chat to null");
            return HandleInputHook.Original(atkModule, inputData, isPadMouseModeEnabled);
        }
    }
}
