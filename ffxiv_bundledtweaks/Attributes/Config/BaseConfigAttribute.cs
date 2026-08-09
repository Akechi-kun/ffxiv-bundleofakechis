using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using System.Reflection;

namespace ComplexTweaks.Attributes.Config;

public abstract class BaseConfigAttribute : Attribute {
    public string Label = string.Empty;
    public string Description = string.Empty;
    public string DependsOn = string.Empty;

    public abstract void Draw(Tweak tweak, object config, FieldInfo fieldInfo);

    protected void OnChangeInternal(Tweak tweak, FieldInfo fieldInfo) {
        tweak.CachedType.GetMethod(nameof(Tweak.OnConfigChangeInternal), BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tweak, [fieldInfo.Name]);
    }

    protected static void DrawConfigInfos(FieldInfo fieldInfo) {
        var attributes = fieldInfo.GetCustomAttributes<ConfigInfoAttribute>();
        if (!attributes.Any())
            return;

        foreach (var attribute in attributes) {
            ImGui.SameLine();
            ImGui.Icon(attribute.Icon, attribute.Color);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(attribute.Description);
        }
    }

    protected static bool DrawResetButton(string defaultValueString) {
        if (string.IsNullOrEmpty(defaultValueString))
            return false;

        ImGui.SameLine();
        return ImGui.IconButton(FontAwesomeIcon.Undo, "##Reset");
    }

    protected static void DrawMissingIpcs(BaseIPC[] missingIpcs) {
        if (missingIpcs.Length == 0)
            return;

        ImGui.SameLine();
        ImGui.Icon(60074, 24);

        using var warningIndent = ImGui.ConfigIndent();
        ImGui.TextV(Colors.Grey2, $"Missing {missingIpcs.Length} of the required plugins for this option to work:");
        foreach (var entry in missingIpcs) {
            ImGui.TextColoredWrapped(Colors.Grey2, $"{entry.Name}:");
            ImGui.SameLine();
            ImGui.CopyableText(entry.Repo);
        }
    }
}
