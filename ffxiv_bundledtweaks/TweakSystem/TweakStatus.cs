using ECommons.ImGuiMethods;

namespace ComplexTweaks.TweakSystem;

public enum TweakStatus {
    Disabled,
    Enabled,
    Error,
    Outdated,
    Disposed,
}

public static class TweakStatusExtensions {
    extension(TweakStatus status) {
        public string GetName() => status switch {
            TweakStatus.Disabled => "Disabled",
            TweakStatus.Enabled => "Enabled",
            TweakStatus.Error => "Initialization Failed",
            TweakStatus.Outdated => "Outdated",
            TweakStatus.Disposed => "Disposed",
            _ => status.ToString(),
        };

        public EzColor GetColor() => status switch {
            TweakStatus.Error or TweakStatus.Outdated => EzColor.RedBright,
            TweakStatus.Enabled => EzColor.GreenBright,
            _ => Colors.Grey3,
        };

        public bool IsTerminal() => status is TweakStatus.Outdated or TweakStatus.Error or TweakStatus.Disposed;
    }
}
