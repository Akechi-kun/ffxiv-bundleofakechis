using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using System.Globalization;
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

    protected static bool TryReset(Tweak tweak, object config, FieldInfo fieldInfo) {
        var defaultValue = fieldInfo.GetValue(Activator.CreateInstance(config.GetType())!);
        if (defaultValue is null)
            return false;

        if (!DrawResetButton(FormatDefault(defaultValue)))
            return false;

        fieldInfo.SetValue(config, defaultValue);
        tweak.CachedType.GetMethod(nameof(Tweak.OnConfigChangeInternal), BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tweak, [fieldInfo.Name]);
        return true;
    }

    protected static bool DrawResetButton(string defaultValueString) {
        if (string.IsNullOrEmpty(defaultValueString))
            return false;

        ImGui.SameLine();
        return ImGui.IconButton(FontAwesomeIcon.Undo, "##Reset");
    }

    protected static string FormatDefault(object value) => value switch {
        float f => string.Format(CultureInfo.InvariantCulture, "{0:0.00}", f),
        int i => string.Format(CultureInfo.InvariantCulture, "{0:0.00}", i),
        string s => s,
        _ => value.ToString() ?? string.Empty,
    };

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
