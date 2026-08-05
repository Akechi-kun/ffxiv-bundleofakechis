using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace ComplexTweaks.Utilities.Extensions;

public static class LuminaExtensions {
    public static unsafe bool IsHeldRaw(this ConfigKey key) {
        if (!key.TryGetInputId(out var inputId)) return false;
        var keybind = UIInputData.Instance()->GetKeybind(inputId);
        foreach (var ks in keybind->KeySettings) {
            if (!IKeyState.Get().IsVirtualKeyValid((VirtualKey)ks.Key)) continue;
            if (IKeyState.Get().GetRawValue((VirtualKey)ks.Key) != 0) return true;
        }
        return false;
    }
    public static unsafe void ResetKeyState(this ConfigKey key) {
        if (key.TryGetInputId(out var inputId)) {
            var keybind = UIInputData.Instance()->GetKeybind(inputId);
            foreach (var ks in keybind->KeySettings) {
                if (!IKeyState.Get().IsVirtualKeyValid((VirtualKey)ks.Key)) continue;
                IKeyState.Get().SetRawValue((VirtualKey)ks.Key, 0);
                if (ks.KeyModifier == KeyModifierFlag.Ctrl)
                    IKeyState.Get().SetRawValue(VirtualKey.CONTROL, 0);
                if (ks.KeyModifier == KeyModifierFlag.Shift)
                    IKeyState.Get().SetRawValue(VirtualKey.LSHIFT, 0);
                if (ks.KeyModifier == KeyModifierFlag.Alt)
                    IKeyState.Get().SetRawValue(VirtualKey.MENU, 0);
            }
        }
    }
}
