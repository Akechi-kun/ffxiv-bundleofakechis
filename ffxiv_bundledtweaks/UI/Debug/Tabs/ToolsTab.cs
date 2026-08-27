using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace ComplexTweaks.UI.Debug.Tabs;

internal class ToolsTab : DebugTab {
    public override void Draw() {
        if (ImGui.Button("Use all items")) {
            var itemIds = CollectUsableItemIds();
            if (itemIds.Count > 0) {
                Svc.Automation.Start(AutoTask.From(async t => {
                    foreach (var itemId in itemIds) {
                        UseItem(itemId);
                        await t.WaitUntil(() => !IObjectTable.Get().LocalPlayer?.IsBusy ?? false, "NotBusy");
                    }
                }, name: "UseAllItems"));
            }
        }

        if (ImGui.Button("hg"))
            ApplyHgStatus();
    }

    private static unsafe List<uint> CollectUsableItemIds() {
        var itemIds = new List<uint>();
        foreach (var c in InventoryType.Bags) {
            var cont = InventoryManager.Instance()->GetInventoryContainer(c);
            for (var i = 0; i < cont->Size; ++i) {
                var slot = cont->GetInventorySlot(i);
                if (Item.TryGetRow(slot->ItemId, out var row) && row.ItemSortCategory.Value.Param is 175 or 160)
                    itemIds.Add(slot->ItemId);
            }
        }
        return itemIds;
    }

    private static unsafe void UseItem(uint itemId) => AgentInventoryContext.Instance()->UseItem(itemId);

    private static unsafe void ApplyHgStatus() {
        var player = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)GameObjectManager.Instance()->Objects.IndexSorted[0].Value;
        player->GetStatusManager()->SetStatus(20, 149, 5.0f, 0, 0xE0000000, true);
    }
}
