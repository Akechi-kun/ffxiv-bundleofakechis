using clib.Ui;
using Dalamud.Interface;

namespace ComplexTweaks.Attributes.Config;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class ConfigInfoAttribute(string label, string desc) : Attribute {
    public string Label { get; init; } = label;
    public string Description { get; init; } = desc;
    public FontAwesomeIcon Icon { get; init; } = FontAwesomeIcon.InfoCircle;
    public Color Color { get; init; } = Colors.Grey;
}
