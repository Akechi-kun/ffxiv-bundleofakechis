using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ComplexTweaks.Attributes.Config;

[AttributeUsage(AttributeTargets.Field)]
public class StringConfigAttribute : BaseConfigAttribute {
    public string IsRegex = string.Empty;

    public override void Draw(Tweak tweak, object config, FieldInfo fieldInfo) {
        var value = (string)fieldInfo.GetValue(config)!;
        var attr = fieldInfo.GetCustomAttribute<BaseConfigAttribute>();

        ImGui.Text(fieldInfo.Name.SplitWords());

        if (ImGui.InputText("##Input", ref value, 500)) {
            fieldInfo.SetValue(config, value);
            OnChangeInternal(tweak, fieldInfo);
        }

        TryReset(tweak, config, fieldInfo);

        // validate regex if IsRegex is set
        if (!string.IsNullOrEmpty(IsRegex) && !string.IsNullOrEmpty(value)) {
            if (config.GetType().GetField(IsRegex) is { } field && field.GetValue(config) is bool b && b) {
                try {
                    _ = new Regex(value);
                    ImGui.SameLine();
                    ImGui.Icon(FontAwesomeIcon.Check, Colors.Valid, "Valid regex pattern");
                }
                catch (ArgumentException) {
                    ImGui.SameLine();
                    ImGui.Icon(FontAwesomeIcon.Ban, Colors.Invalid, "Invalid regex pattern");
                }
            }
        }

        if (!attr?.Description.IsEmpty ?? false)
            ImGui.TextColoredWrapped(Colors.Grey, attr!.Description);
    }
}
