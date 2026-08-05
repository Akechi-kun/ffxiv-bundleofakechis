using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace ComplexTweaks.Tweaks;

[Tweak]
public partial class AntiAFK : Tweak {
    public override string Name => "Anti-AFK";
    public override string Description => "Prevents being kicked for being AFK.";

    [SigHook("E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 8B 01 FF 90 ?? ?? ?? ?? 84 C0")]
    internal unsafe void InputTimerModule_Update(InputTimerModule* thisPtr, float delta) {
        thisPtr->ResetAfkTimer();
        InputTimerModule_UpdateHook.Original(thisPtr, delta);
    }
}
