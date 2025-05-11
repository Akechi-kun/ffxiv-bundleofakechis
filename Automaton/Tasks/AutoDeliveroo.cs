using Automaton.Features;
using Dalamud.Plugin.Ipc.Exceptions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System.Threading.Tasks;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Automaton.Tasks;
public sealed class AutoDeliveroo(ARTurnInConfiguration? Config = null) : CommonTasks
{
    protected override async Task Execute()
    {
        Status = "Going to GC";
        await GoToGC();
        if (Config is { EquipGearsetterRecs: true })
        {
            Status = "Updating Gearsets";
            await EquipGearsetterUpgrades();
        }
        Status = "Turning in Gear";
        await TurnIn();
        Status = "Going Home";
        await GoHome();
    }

    private async Task GoToGC()
    {
        using var scope = BeginScope("GoToGC");
        Service.Lifestream.ExecuteCommand("gc");
        await WaitUntilThenFalse(() => Service.Lifestream.IsBusy(), $"{nameof(GoToGC)}");
    }

    /*
     * Problems with this approach:
     * - Inventory outside of armoury chest isn't considered
     * - Could potentially overwrite gearsets on valuable characters (meant for an alt-only thing where they can gear up based on what they bring back from ventures)
     */
    private async Task EquipRecommended()
    {
        using var scope = BeginScope("EquipRecommended");
        var updating = false;
        unsafe
        {
            var mod = RecommendEquipModule.Instance();
            if (mod == null) return;
            updating = mod->IsUpdating;
        }
        await WaitUntil(() => !updating, $"WaitingFor{nameof(RecommendEquipModule)}Update");

        unsafe
        {
            var mod = RecommendEquipModule.Instance();
            var equippedItems = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            var isAllEquipped = true;
            foreach (var recommendedItemPtr in mod->RecommendedItems)
            {
                var recommendedItem = recommendedItemPtr.Value;
                if (recommendedItem == null || recommendedItem->ItemId == 0)
                    continue;

                var isEquipped = false;
                for (var i = 0; i < equippedItems->Size; ++i)
                {
                    var equippedItem = equippedItems->Items[i];
                    if (equippedItem.ItemId != 0 && equippedItem.ItemId == recommendedItem->ItemId)
                    {
                        isEquipped = true;
                        break;
                    }
                }

                if (!isEquipped)
                    isAllEquipped = false;
            }

            if (!isAllEquipped)
                mod->EquipRecommendedGear();

        }
        await WaitUntil(() => !Player.IsBusy, $"WaitingForNotBusy");
    }

    private async Task EquipGearsetterUpgrades()
    {
        using var scope = BeginScope("EquipGearsetterUpgrades");

        try
        {
            var test = GetGearsetRecommendations();
        }
        catch (IpcNotReadyError)
        {
            Log($"Skipping {nameof(EquipGearsetterUpgrades)}, {nameof(GearsetterIPC)} not ready.");
            return;
        }

        foreach (var gearset in GetValidGearsets())
        {
            if (GetGearsetRecommendations(gearset) is { Count: > 0 } recs)
            {
                if (TryEquipGearset(gearset))
                {
                    Log($"Equipped gearset #{gearset} {GetGearsetName(gearset)}");
                    await WaitUntil(() => Player.JobId == GetGearsetClassJob(gearset), "WaitForJobChange");
                    foreach ((var itemId, var sourceInventoryType, var sourceInventorySlot, var targetEquipSlot) in GetGearsetRecommendations(gearset))
                    {
                        if (sourceInventoryType is { } cont && sourceInventorySlot is { } slot)
                            await EquipItem(GetRow<Item>(itemId) ?? throw new Exception($"Item #{itemId} not found"), cont, slot, (uint)targetEquipSlot);
                        else Log($"Skipping #{itemId}. inv?: {sourceInventoryType is null}; slot?: {sourceInventorySlot is null}");
                        await NextFrame();
                    }
                    UpdateCurrentGearset();
                }
                else
                    Error($"Failed to equip gearset #{gearset}");
            }
            else Log($"Skipping gearset #{gearset} {GetGearsetName(gearset)}: no recommendations.");
        }
    }

    private async Task EquipItem(Item item, InventoryType cont, byte slot, uint targetSlot)
    {
        using var scope = BeginScope("EquipItem");
        Log($"Equipping [#{item.RowId} {item.Name}] from {cont}:{slot} [{Inventory.GetItemInSlot(cont, slot)?.Name}] to {targetSlot} [{Inventory.GetItemInSlot(InventoryType.EquippedItems, (int)targetSlot)?.Name}]");
        if (FindAndEquip(item, cont, targetSlot))
            await WaitUntil(() => ItemIsEquipped(item.RowId, (int)targetSlot), $"WaitingForEquipped_#{item.RowId}");
    }

    private async Task TurnIn()
    {
        using var scope = BeginScope("TurnIn");
        Svc.Commands.ProcessCommand("/deliveroo enable");
        await WaitUntilThenFalse(() => Service.Deliveroo.IsTurnInRunning(), $"{nameof(TurnIn)}");
    }

    private async Task GoHome()
    {
        using var scope = BeginScope("GoHome");
        Service.Lifestream.ExecuteCommand("auto");
        await WaitUntilThenFalse(() => Service.Lifestream.IsBusy(), $"{nameof(GoHome)}");
    }

    private unsafe string GetGearsetName(byte? index = null) => RaptureGearsetModule.Instance()->GetGearset(index ?? RaptureGearsetModule.Instance()->CurrentGearsetIndex)->NameString;
    private unsafe byte GetGearsetClassJob(byte? index = null) => RaptureGearsetModule.Instance()->GetGearset(index ?? RaptureGearsetModule.Instance()->CurrentGearsetIndex)->ClassJob;
    private unsafe bool TryEquipGearset(byte id)
        => RaptureGearsetModule.Instance()->CurrentGearsetIndex == id || RaptureGearsetModule.Instance()->EquipGearset(id) == 0;

    private unsafe List<(uint itemId, InventoryType? inventoryType, byte? sourceInventorySlot, RaptureGearsetModule.GearsetItemIndex targetSlot)> GetGearsetRecommendations(byte? index = null)
        => Service.Gearsetter.GetRecommendationsForGearset(index ?? (byte)RaptureGearsetModule.Instance()->CurrentGearsetIndex);
    private unsafe bool FindAndEquip(Item item, InventoryType inventoryType, uint equipSlot) => FindAndEquip(item, inventoryType, equipSlot, GetItemSorter(item.EquipSlotCategory.Value));
    private unsafe bool FindAndEquip(Item item, InventoryType inventoryType, uint equipSlot, ItemOrderModuleSorter* sorter)
    {
        // TODO: fix redundancies
        var inventoryManager = InventoryManager.Instance();
        //for (var i = 0U; i < sorter->Items.LongCount; i++)
        //{
        //    var entry = sorter->Items[i].Value;
        //    var invItem = inventoryManager->GetInventorySlot(inventoryType + entry->Page, entry->Slot);
        //    if (invItem->ItemId == item.RowId)
        //    {
        //        var page = (uint)(i / sorter->ItemsPerPage);
        //        var slot = (uint)(i % sorter->ItemsPerPage);
        //        Log($"#{item.RowId} [{(uint)inventoryType} -> {page} | {slot}]");
        //        MoveItem(inventoryType + page, slot, equipSlot);
        //        return true;
        //    }
        //}

        foreach (var inv in Inventory.Equippable)
        {
            var cont = inventoryManager->GetInventoryContainer(inv);
            for (var i = 0; i < cont->Size; ++i)
            {
                var invItem = cont->GetInventorySlot(i);
                if (invItem->ItemId == item.RowId)
                {
                    // it target item's armoury container is full, move a non-gearset item out, if it can't be done, skip and log a warning
                    var discardInv = GetItemArmouryContainer(item);
                    if (Inventory.GetEmptySlots(discardInv) == 0)
                    {
                        var emptySlot = Inventory.GetFirstEmptySlot();
                        if (emptySlot is null)
                        {
                            Svc.Log.Warning($"Source item's armoury container is full. No room in inventory to move non-gearset item out.");
                            return false;
                        }

                        var nonGearsetItem = Inventory.GetFirstNonGearsetItem(discardInv);
                        if (nonGearsetItem is null)
                        {
                            Svc.Log.Warning($"Source item's armoury container is full. No non-gearset item to move out.");
                            return false;
                        }

                        var discardContainer = inventoryManager->GetInventoryContainer(discardInv);
                        Log($"Upgrade item requires free armoury slot to equip. Moving #{nonGearsetItem->ItemId} [{(uint)discardInv} -> {emptySlot->Slot}].");
                        MoveItem(discardInv, (uint)nonGearsetItem->Slot, (uint)emptySlot->Slot, emptySlot->Container);
                    }

                    // equip item
                    Log($"#{item.RowId} [{(uint)inv} -> 0 | {i}]");
                    MoveItem(inv, (uint)i, equipSlot);
                    return true;
                }
            }
        }
        return false;
    }
    private unsafe void MoveItem(InventoryType sourceInventory, uint sourceSlot, uint equipSlot, InventoryType? destInventory = null)
    {
        // from simpletweaks
        var sourceContainerId = GetContainerId(sourceInventory);
        var destinationContainerId = GetContainerId(destInventory ?? InventoryType.EquippedItems);
        if (sourceContainerId != 0 && destinationContainerId != 0)
        {
            var eis = stackalloc AtkValue[4];
            var dropOut = stackalloc byte[32];
            for (var i = 0; i < 4; i++) eis[i].Type = ValueType.UInt;
            eis[0].UInt = sourceContainerId;
            eis[1].UInt = sourceSlot;
            eis[2].UInt = destinationContainerId;
            eis[3].UInt = equipSlot;
            var atkModule = RaptureAtkModule.Instance();
            if (Service.Memory.MoveItem is { } moveItem)
                moveItem.Invoke(atkModule, dropOut, eis);
            else Error($"MoveItem delegate not found");
        }
    }
    private unsafe bool ItemIsEquipped(uint itemId, int slot) => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[slot].ItemId == itemId;
    private unsafe void UpdateCurrentGearset() => RaptureGearsetModule.Instance()->UpdateGearset(RaptureGearsetModule.Instance()->CurrentGearsetIndex);

    private unsafe List<byte> GetValidGearsets()
    {
        var gm = RaptureGearsetModule.Instance();
        if (gm is null) return [];
        List<byte> gearsets = [];
        for (byte i = 0; i < 100; ++i)
        {
            if (!gm->IsValidGearset(i)) continue;
            var gearset = gm->GetGearset(i);
            if (gearset != null && gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) && GetRow<ClassJob>(gearset->ClassJob)?.Unknown8 != 0)
                if (gearset->NameString.Split((char)0)[0] is { } name && !name.ContainsAny(StringComparison.OrdinalIgnoreCase, "Eureka", "Bozja", "_"))
                    gearsets.Add(i);
        }
        return gearsets;
    }

    private uint GetContainerId(InventoryType inventoryType) => inventoryType switch
    {
        InventoryType.Inventory1 => 48,
        InventoryType.Inventory2 => 49,
        InventoryType.Inventory3 => 50,
        InventoryType.Inventory4 => 51,
        InventoryType.ArmoryMainHand => 57,
        InventoryType.ArmoryHead => 58,
        InventoryType.ArmoryBody => 59,
        InventoryType.ArmoryHands => 60,
        InventoryType.ArmoryLegs => 61,
        InventoryType.ArmoryFeets => 62,
        InventoryType.ArmoryOffHand => 63,
        InventoryType.ArmoryEar => 64,
        InventoryType.ArmoryNeck => 65,
        InventoryType.ArmoryWrist => 66,
        InventoryType.ArmoryRings => 67,
        InventoryType.ArmorySoulCrystal => 68,
        InventoryType.EquippedItems => 4,
        _ => 0
    };

    private unsafe InventoryType GetItemArmouryContainer(Item item) => item.EquipSlotCategory.Value switch
    {
        { MainHand: 1 } => InventoryType.ArmoryMainHand,
        { OffHand: 1 } => InventoryType.ArmoryOffHand,
        { Head: 1 } => InventoryType.ArmoryHead,
        { Body: 1 } => InventoryType.ArmoryBody,
        { Gloves: 1 } => InventoryType.ArmoryHands,
        { Legs: 1 } => InventoryType.ArmoryLegs,
        { Feet: 1 } => InventoryType.ArmoryFeets,
        { Ears: 1 } => InventoryType.ArmoryEar,
        { Neck: 1 } => InventoryType.ArmoryNeck,
        { Wrists: 1 } => InventoryType.ArmoryWrist,
        { FingerL: 1 } => InventoryType.ArmoryRings,
        { FingerR: 1 } => InventoryType.ArmoryRings,
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
    };

    private unsafe ItemOrderModuleSorter* GetItemSorter(EquipSlotCategory esc) => esc switch
    {
        { MainHand: 1 } => ItemOrderModule.Instance()->ArmouryMainHandSorter,
        { OffHand: 1 } => ItemOrderModule.Instance()->ArmouryOffHandSorter,
        { Head: 1 } => ItemOrderModule.Instance()->ArmouryHeadSorter,
        { Body: 1 } => ItemOrderModule.Instance()->ArmouryBodySorter,
        { Gloves: 1 } => ItemOrderModule.Instance()->ArmouryHandsSorter,
        { Legs: 1 } => ItemOrderModule.Instance()->ArmouryLegsSorter,
        { Feet: 1 } => ItemOrderModule.Instance()->ArmouryFeetSorter,
        { Ears: 1 } => ItemOrderModule.Instance()->ArmouryEarsSorter,
        { Neck: 1 } => ItemOrderModule.Instance()->ArmouryNeckSorter,
        { Wrists: 1 } => ItemOrderModule.Instance()->ArmouryWristsSorter,
        { FingerL: 1 } => ItemOrderModule.Instance()->ArmouryRingsSorter,
        { FingerR: 1 } => ItemOrderModule.Instance()->ArmouryRingsSorter,
        _ => ItemOrderModule.Instance()->InventorySorter,
    };
}
