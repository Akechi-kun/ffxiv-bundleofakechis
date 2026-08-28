using clib.Ui;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ComplexTweaks.Utilities;

public static class Colors {
    public static Color Grey { get; } = new(0.73f, 0.73f, 0.73f);
    public static Color Grey2 { get; } = new(0.87f, 0.87f, 0.87f);
    public static Color Grey3 { get; } = new(0.6f, 0.6f, 0.6f);
    public static Color Grey4 { get; } = new(0.3f, 0.3f, 0.3f);
    public static Color Type { get; } = new(0.2f, 0.9f, 0.9f);
    public static Color Field { get; } = new(0.2f, 0.9f, 0.4f);

    public static Color Valid { get; } = Color.FromUInt(0x00AA00FF, ColorFormat.Abgr);
    public static Color Invalid { get; } = Color.FromUInt(0xAA0000FF, ColorFormat.Abgr);

    public static Color Positive { get; } = new(0.22f, 0.45f, 0.24f);
    public static Color PositiveHover { get; } = new(0.27f, 0.53f, 0.29f);
    public static Color PositiveActive { get; } = new(0.19f, 0.39f, 0.21f);
    public static Color Negative { get; } = new(0.55f, 0.2f, 0.2f);
    public static Color NegativeHover { get; } = new(0.62f, 0.24f, 0.24f);
    public static Color NegativeActive { get; } = new(0.5f, 0.18f, 0.18f);
    public static Color ChipPositive { get; } = new(0.16f, 0.34f, 0.2f);
    public static Color ChipMuted { get; } = new(0.25f, 0.25f, 0.25f);
    public static Color ChipGold { get; } = new(0.42f, 0.35f, 0.2f);
    public static Color ChipPrimary { get; } = new(0.25f, 0.22f, 0.37f);
    public static Color ChipInfo { get; } = new(0.18f, 0.3f, 0.4f);

    public static unsafe bool IsLightTheme
        => RaptureAtkModule.Instance()->AtkUIColorHolder.ActiveColorThemeType == 1;
}
