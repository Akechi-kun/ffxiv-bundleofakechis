using clib.Ui;
using Dalamud.Bindings.ImGui;

namespace ComplexTweaks.UI.Debug.Tabs;

internal class InstalledPluginsTab : DebugTab {
    public override void Draw() {
        foreach (var plugin in Svc.Interface.InstalledPlugins) {
            ImGui.Text($"[{plugin.InternalName}] {plugin.Name} <{plugin.Version}>");
            ImGui.SameLine();
            ImGui.TextColored(plugin.IsLoaded ? Color.Green : Color.Red, "Loaded");
        }
    }
}
