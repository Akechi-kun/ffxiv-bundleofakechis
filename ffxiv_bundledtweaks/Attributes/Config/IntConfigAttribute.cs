using Dalamud.Bindings.ImGui;
using System.Reflection;

namespace ComplexTweaks.Attributes.Config;

[AttributeUsage(AttributeTargets.Field)]
public class IntConfigAttribute : BaseConfigAttribute {
    public int Min = 0;
    public int Max = 100;
    public bool SameLine = false;

    public override void Draw(Tweak tweak, object config, FieldInfo fieldInfo) {
        var value = (int)fieldInfo.GetValue(config)!;
        var attr = fieldInfo.GetCustomAttribute<BaseConfigAttribute>();

        ImGui.TextV(fieldInfo.Name.SplitWords());
        if (SameLine) ImGui.SameLine();

        using var indent = ImGui.ConfigIndent(!SameLine);

        if (ImGui.DragInt("##Input", ref value, 0.01f, Min, Max)) {
            fieldInfo.SetValue(config, value);
            OnChangeInternal(tweak, fieldInfo);
        }

        TryReset(tweak, config, fieldInfo);

        if (!attr?.Description.IsEmpty ?? false)
            ImGui.TextColoredWrapped(Colors.Grey, attr!.Description);
    }
}
