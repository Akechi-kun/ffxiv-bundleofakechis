using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace ComplexTweaks.UI.Debug.Tabs;

internal unsafe class ToolsTab : DebugTab {
    public override void Draw() {
        if (ImGui.Button("Use all items")) {
            foreach (var c in InventoryType.Bags) {
                var cont = InventoryManager.Instance()->GetInventoryContainer(c);
                for (var i = 0; i < cont->Size; ++i) {
                    var slot = cont->GetInventorySlot(i);
                    if (Item.TryGetRow(slot->ItemId, out var row) && row.ItemSortCategory.Value.Param is 175 or 160) {
                        Service.TaskManager.Enqueue(() => AgentInventoryContext.Instance()->UseItem(slot->ItemId));
                        Service.TaskManager.Enqueue(() => !IObjectTable.Get().LocalPlayer?.IsBusy ?? false);
                    }
                    //ActionManager.Instance()->UseAction(ActionType.Item, slot->ItemId);
                }
            }
        }

        if (ImGui.Button("hg")) {
            var player = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)GameObjectManager.Instance()->Objects.IndexSorted[0].Value;
            player->GetStatusManager()->SetStatus(20, 149, 5.0f, 0, 0xE0000000, true);
        }

        if (ImGui.Button("leave content"))
            EventFramework.LeaveCurrentContent(true);
    }
}
