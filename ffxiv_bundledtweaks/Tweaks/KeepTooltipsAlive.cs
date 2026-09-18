using Dalamud.Bindings.ImGui;

namespace ComplexTweaks.Tweaks;

public class KeepTooltipsAlive : Tweak {
    public override string Name => "Keep Tooltips Alive";
    public override string Description => "Prevents tooltips from closing when you stop hovering the item if you hold ctrl.";

    public override void OnEnable() => IAddonLifecycle.Get().RegisterListener(AddonEvent.PreHide, "ItemDetail", PreventClosing);
    public override void OnDisable() => IAddonLifecycle.Get().UnregisterListener(PreventClosing);

    private void PreventClosing(AddonEvent type, AddonArgs args) {
        if (ImGui.GetIO().KeyCtrl)
            args.PreventOriginal();
    }
}
