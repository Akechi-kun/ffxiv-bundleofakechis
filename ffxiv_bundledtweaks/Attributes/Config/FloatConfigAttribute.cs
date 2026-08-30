using Dalamud.Bindings.ImGui;
using System.Reflection;

namespace ComplexTweaks.Attributes.Config;

[AttributeUsage(AttributeTargets.Field)]
public class FloatConfigAttribute : BaseConfigAttribute {
    public float Min = 0;
    public float Max = 100;

    public override void Draw(Tweak tweak, object config, FieldInfo fieldInfo) {
        var value = (float)fieldInfo.GetValue(config)!;
        var attr = fieldInfo.GetCustomAttribute<BaseConfigAttribute>();

        ImGui.Text(fieldInfo.Name.SplitWords());

        using var indent = ImGui.ConfigIndent();

        if (ImGui.DragFloat("##Input", ref value, 0.01f, Min, Max, "%.2f")) {
            fieldInfo.SetValue(config, value);
            OnChangeInternal(tweak, fieldInfo);
        }

        TryReset(tweak, config, fieldInfo);

        if (!attr?.Description.IsEmpty ?? false)
            ImGui.TextColoredWrapped(Colors.Grey, attr!.Description);
    }
}
