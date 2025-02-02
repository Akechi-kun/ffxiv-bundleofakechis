using ImGuiNET;

namespace Automaton.UI.Debug.Tabs;
internal class InstalledPluginsTab : DebugTab
{
    public override void Draw()
    {
        foreach (var plugin in Svc.PluginInterface.InstalledPlugins)
        {
            ImGui.TextUnformatted($"[{plugin.InternalName}] {plugin.Name} <{plugin.Version}>");
            ImGui.SameLine();
            ImGui.TextColored(plugin.IsLoaded ? Colors.Green : Colors.Red, "Loaded");
        }
    }
}
