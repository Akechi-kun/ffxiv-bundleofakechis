using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using System.Reflection;

namespace ComplexTweaks.Attributes.Config;

[AttributeUsage(AttributeTargets.Field)]
public class EnumConfigAttribute : BaseConfigAttribute {
    public bool NoLabel = false;

    public override void Draw(Tweak tweak, object config, FieldInfo fieldInfo) {
        var enumType = fieldInfo.FieldType;
        var attr = fieldInfo.GetCustomAttribute<BaseConfigAttribute>();
        var fieldMissing = Service.IPC.GetMissing(fieldInfo);
        var selectedValue = (Enum)fieldInfo.GetValue(config)!;
        var selectedMissing = Service.IPC.GetMissing(selectedValue);
        var missingIpcs = fieldMissing.Concat(selectedMissing).Distinct().ToArray();

        string GetOptionLabel(Enum value) => value.ToString();

        if (!NoLabel) {
            ImGui.Text(!attr?.Label.IsEmpty ?? false ? attr!.Label : fieldInfo.Name.SplitWords());
        }

        using var indent = ImGui.ConfigIndent(!NoLabel);

        var labels = Enum.GetNames(enumType);
        var comboWidth = labels.Max(n => ImGui.CalcTextSize(n).X) + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X * 2;
        ImGui.SetNextItemWidth(comboWidth);

        using (var combo = ImRaii.Combo("##Input", GetOptionLabel(selectedValue))) {
            if (combo.Success) {
                foreach (var name in labels) {
                    var value = (Enum)Enum.Parse(enumType, name);
                    var valueMissing = Service.IPC.GetMissing(value);
                    var unavailable = valueMissing.Length > 0;

                    if (ImGui.Selectable(GetOptionLabel(value), Equals(selectedValue, value), unavailable ? ImGuiSelectableFlags.Disabled : ImGuiSelectableFlags.None) && !unavailable) {
                        fieldInfo.SetValue(config, value);
                        OnChangeInternal(tweak, fieldInfo);
                    }

                    if (unavailable && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip($"Requires: {string.Join(", ", valueMissing.Select(m => m.Name))}");

                    if (Equals(selectedValue, value))
                        ImGui.SetItemDefaultFocus();
                }
            }
        }

        DrawMissingIpcs(missingIpcs);

        if (!attr?.Description.IsEmpty ?? false)
            ImGui.TextColoredWrapped(Colors.Grey, attr!.Description);
    }
}
