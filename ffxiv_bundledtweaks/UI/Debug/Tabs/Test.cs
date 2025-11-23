using Dalamud.Bindings.ImGui;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace ComplexTweaks.UI.Debug.Tabs;
internal unsafe class TestTab : DebugTab
{
    public override void Draw()
    {
        var x = *((byte*)InfoProxyNoviceNetwork.Instance() + 0x18);
        ImGuiEx.Text($"nn: {x} {x:B8}");
        ImGuiEx.Text($"{Svc.PluginInterface.InternalName}: {Svc.PluginInterface.GetPluginConfigDirectory()}");

        if (ImGui.Button($"compress"))
            ImGui.SetClipboardText(ImGui.GetClipboardText().ToBase64());
        if (ImGui.Button($"decompress"))
            ImGui.SetClipboardText(ImGui.GetClipboardText().FromBase64());
    }
}
