using Dalamud.Interface.Components;
using Dalamud.Bindings.ImGui;
using System.Reflection;

namespace ComplexTweaks.Attributes.Config;

[AttributeUsage(AttributeTargets.Field)]
public class ColorConfigAttribute : BaseConfigAttribute {
    public ImGuiColorEditFlags Flags = ImGuiColorEditFlags.NoAlpha;

    public override void Draw(Tweak tweak, object config, FieldInfo fieldInfo) {
        var value = (Vector4)fieldInfo.GetValue(config)!;
        var attr = fieldInfo.GetCustomAttribute<BaseConfigAttribute>();

        ImGui.TextV(fieldInfo.Name.SplitWords());
        ImGui.SameLine();

        var newColor = ImGuiComponents.ColorPickerWithPalette(1, $"##{fieldInfo.Name}", value, Flags);
        if (!value.Equals(newColor)) {
            fieldInfo.SetValue(config, newColor);
            OnChangeInternal(tweak, fieldInfo);
        }

        TryReset(tweak, config, fieldInfo);

        DrawConfigInfos(fieldInfo);

        if (!attr?.Description.IsEmpty ?? false)
            ImGui.TextColoredWrapped(Colors.Grey, attr!.Description);
    }
}
