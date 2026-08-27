using FFXIVClientStructs.FFXIV.Client.Game;

namespace ComplexTweaks.Tweaks;

public class AutoMerge : Tweak {
    public override string Name => "Auto Merge";
    public override string Description => "Merge incomplete stacks upon opening your inventory.";

    public override void OnEnable() => IAddonLifecycle.Get().RegisterListener(AddonEvent.PostShow, ["InventoryExpansion", "InventoryLarge", "Inventory", "AetherBags_MainBags"], OnSetup);
    public override void OnDisable() => IAddonLifecycle.Get().UnregisterListener(OnSetup);

    private unsafe void OnSetup(AddonEvent type, AddonArgs args) {
        try {
            if (IObjectTable.Get().LocalPlayer.IsBusy || !ICondition.Get().CanMoveItems()) return;
            var inv = InventoryManager.Instance();

            var incompleteStacks = InventoryType.Bags
                .SelectMany(container => inv->GetItems(container))
                .Where(handle => handle.ItemId != 0
                    && !handle.IsCollectible
                    && handle.ItemLocation != null
                    && handle.ItemLocation.GetInventoryItem() != null
                    && handle.ItemLocation.GetInventoryItem()->Quantity < handle.GameData.ValueNullable?.StackSize)
                .GroupBy(handle => new { handle.ItemId, handle.IsHighQuality })
                .Where(group => group.Count() > 1);

            foreach (var group in incompleteStacks) {
                var firstSlot = group.First();
                if (firstSlot.ItemLocation == null) continue;

                foreach (var slot in group.Skip(1)) {
                    if (slot.ItemLocation == null) continue;
                    inv->MoveItemSlot(slot.ItemLocation.Container, slot.ItemLocation.Slot,
                        firstSlot.ItemLocation.Container, firstSlot.ItemLocation.Slot, true);
                }
            }
        }
        catch (Exception ex) { Error(ex, "Error during auto-merge"); }
    }
}
