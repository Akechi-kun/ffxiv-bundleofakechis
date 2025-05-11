using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;

namespace Automaton.Utilities;

public unsafe class Inventory
{
    public static readonly InventoryType[] PlayerInventory =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
        InventoryType.KeyItems,
    ];

    public static readonly InventoryType[] MainOffHand =
    [
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryOffHand
    ];

    public static readonly InventoryType[] LeftSideArmory =
    [
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets
    ];

    public static readonly InventoryType[] RightSideArmory =
    [
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings
    ];

    public static readonly InventoryType[] Armory = [.. MainOffHand, .. LeftSideArmory, .. RightSideArmory, InventoryType.ArmorySoulCrystal];
    public static readonly InventoryType[] Equippable = [.. PlayerInventory, .. Armory];

    public static unsafe (InventoryType inv, int slot)? GetItemLocationInInventory(uint itemId, IEnumerable<InventoryType> inventories)
    {
        foreach (var inv in inventories)
        {
            var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
            for (var i = 0; i < cont->Size; ++i)
                if (cont->GetInventorySlot(i)->ItemId == itemId)
                    return (inv, i);
        }
        return null;
    }

    private static unsafe int InternalGetItemCount(uint itemId, bool isHq) => InventoryManager.Instance()->GetInventoryItemCount(itemId, isHq);
    public static unsafe int GetItemCount(uint itemId, bool includeHQ = true) => includeHQ ? InternalGetItemCount(itemId, true) + InternalGetItemCount(itemId, false) : InternalGetItemCount(itemId, false);

    public static unsafe bool HasItem(uint itemId) => GetItemInInventory(itemId, Equippable) != null;
    public static unsafe bool HasItemEquipped(uint itemId)
    {
        var cont = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < cont->Size; ++i)
            if (cont->GetInventorySlot(i)->ItemId == itemId)
                return true;
        return false;
    }

    public static unsafe InventoryItem* GetItemInInventory(uint itemId, IEnumerable<InventoryType> inventories, bool mustBeHQ = false)
    {
        foreach (var inv in inventories)
        {
            var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
            for (var i = 0; i < cont->Size; ++i)
                if (cont->GetInventorySlot(i)->ItemId == itemId && (!mustBeHQ || cont->GetInventorySlot(i)->Flags == InventoryItem.ItemFlags.HighQuality))
                    return cont->GetInventorySlot(i);
        }
        return null;
    }

    public static unsafe List<Pointer<InventoryItem>> GetHQItems(IEnumerable<InventoryType> inventories)
    {
        List<Pointer<InventoryItem>> items = [];
        foreach (var inv in inventories)
        {
            var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
            for (var i = 0; i < cont->Size; ++i)
                if (cont->GetInventorySlot(i)->Flags == InventoryItem.ItemFlags.HighQuality)
                    items.Add(cont->GetInventorySlot(i));
        }
        return items;
    }

    public static unsafe List<Pointer<InventoryItem>> GetDesynthableItems(IEnumerable<InventoryType> inventories)
    {
        List<Pointer<InventoryItem>> items = [];
        foreach (var inv in inventories)
        {
            var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
            for (var i = 0; i < cont->Size; ++i)
                if (GetRow<Item>(cont->GetInventorySlot(i)->ItemId)?.Desynth > 0)
                    items.Add(cont->GetInventorySlot(i));
        }
        return items;
    }

    public static unsafe uint GetEmptySlots(InventoryType inv) => GetEmptySlots([inv]);
    public static unsafe uint GetEmptySlots(IEnumerable<InventoryType> inventories = null)
    {
        if (inventories == null)
            return InventoryManager.Instance()->GetEmptySlotsInBag();
        else
        {
            uint count = 0;
            foreach (var inv in inventories)
            {
                var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
                for (var i = 0; i < cont->Size; ++i)
                    if (cont->GetInventorySlot(i)->ItemId == 0)
                        count++;
            }
            return count;
        }
    }

    public static unsafe Item? GetItemInSlot(InventoryType inv, int slot)
        => GetRow<Item>(InventoryManager.Instance()->GetInventoryContainer(inv)->GetInventorySlot(slot)->ItemId).Value;

    public static unsafe InventoryItem* GetFirstEmptySlot(InventoryType? inv = null)
    {
        if (inv is null)
        {
            foreach (var i in PlayerInventory)
            {
                if (i == InventoryType.KeyItems) continue;
                var cont = InventoryManager.Instance()->GetInventoryContainer(i);
                for (var j = 0; j < cont->Size; ++j)
                    if (cont->GetInventorySlot(j)->ItemId == 0)
                        return cont->GetInventorySlot(j);
            }
        }
        else
        {
            var cont = InventoryManager.Instance()->GetInventoryContainer(inv.Value);
            for (var i = 0; i < cont->Size; ++i)
                if (cont->GetInventorySlot(i)->ItemId == 0)
                    return cont->GetInventorySlot(i);
        }
        return null;
    }

    public static List<uint>? GetGearsetItemIds()
    {
        var gm = RaptureGearsetModule.Instance();
        List<uint> itemIds = [];
        for (byte i = 0; i < 100; ++i)
        {
            if (!gm->IsValidGearset(i)) continue;
            var gearset = gm->GetGearset(i);
            if (gearset != null && gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) && GetRow<ClassJob>(gearset->ClassJob)?.Unknown8 != 0)
                itemIds.AddRange(gearset->Items.ToArray().Where(x => x.ItemId != 0).Select(x => x.ItemId));
        }
        return itemIds.Count == 0 ? null : itemIds;
    }

    public static unsafe InventoryItem* GetFirstNonGearsetItem(InventoryType inv)
    {
        var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
        var gearsetItems = GetGearsetItemIds();
        for (var i = 0; i < cont->Size; ++i)
            if (gearsetItems?.Contains(cont->GetInventorySlot(i)->ItemId) == false)
                return cont->GetInventorySlot(i);
        return null;
    }
}
