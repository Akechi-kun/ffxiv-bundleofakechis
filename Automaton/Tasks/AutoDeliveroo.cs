using Automaton.Features;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Ipc.Exceptions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentChatLog.Delegates;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Automaton.Tasks;
public sealed class AutoDeliveroo(ARTurnInConfiguration? Config = null) : CommonTasks
{
    private readonly Memory.ClassJobInfoSetupIPCReceive ipc = new();
    protected override async Task Execute()
    {
        Status = "Going to GC";
        await GoToGC();
        //if (Config is { EquipGearsetterRecs: true })
        //{
        //    Status = "Updating Gearsets";
        //    await EquipGearsetterUpgrades();
        //}
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
            if (TryEquipGearset(gearset))
            {
                Log($"Equipped gearset #{gearset} {GetGearsetName(gearset)}");
                await WaitUntil(() => Player.JobId == GetGearsetClassJob(gearset), "WaitForJobChange");
                if (GetGearsetRecommendations() is { Count: > 0 } recs)
                {
                    await NextFrame();
                    foreach ((var itemId, var sourceInventoryType, var sourceInventorySlot, var targetEquipSlot) in GetGearsetRecommendations())
                    {
                        if (sourceInventoryType is { } cont && sourceInventorySlot is { } slot)
                            await EquipItem(GetRow<Item>(itemId) ?? throw new Exception($"Item #{itemId} not found"), cont, slot, (uint)targetEquipSlot);
                        else Log($"Skipping #{itemId}. inv?: {sourceInventoryType is null}; slot?: {sourceInventorySlot is null}");
                        await NextFrame();
                    }
                    UpdateCurrentGearset();
                }
                else Log($"Skipping gearset #{gearset} {GetGearsetName}: no recommendations.");
            }
            else
                Error($"Failed to equip gearset #{gearset}");
        }
    }

    private async Task EquipItem(Item item, InventoryType cont, byte slot, uint targetSlot)
    {
        using var scope = BeginScope("EquipItem");
        Log($"Equipping [#{item.RowId} {item.Name}] from {cont}:{slot} [{Inventory.GetItemInSlot(cont, slot)?.Name}] to {targetSlot} [{Inventory.GetItemInSlot(InventoryType.EquippedItems, (int)targetSlot)?.Name}]");
        if (FindAndEquip(item, cont, targetSlot))
            await WaitUntil(() => ItemIsEquipped(item.RowId, (int)targetSlot), $"WaitingForEquipped_#{item.RowId}");
        //MoveItem(item, cont, slot, targetSlot); // TODO: needs checking if armoury container has free space
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

    private unsafe List<(uint itemId, InventoryType? inventoryType, byte? sourceInventorySlot, RaptureGearsetModule.GearsetItemIndex targetSlot)> GetGearsetRecommendations()
        => Service.Gearsetter.GetRecommendationsForGearset((byte)RaptureGearsetModule.Instance()->CurrentGearsetIndex);
    private unsafe bool FindAndEquip(Item item, InventoryType inventoryType, uint equipSlot) => FindAndEquip(item, inventoryType, equipSlot, GetItemSorter(item.EquipSlotCategory.Value));
    private unsafe bool FindAndEquip(Item item, InventoryType inventoryType, uint equipSlot, ItemOrderModuleSorter* sorter)
    {
        var inventoryManager = InventoryManager.Instance();
        for (var i = 0U; i < sorter->Items.LongCount; i++)
        {
            var entry = sorter->Items[i].Value;
            var invItem = inventoryManager->GetInventorySlot(inventoryType + entry->Page, entry->Slot);
            if (invItem->ItemId == item.RowId)
            {
                var page = (uint)(i / sorter->ItemsPerPage);
                var slot = (uint)(i % sorter->ItemsPerPage);
                Log($"#{item.RowId} [{(uint)inventoryType} -> {page} | {slot}]");
                MoveItem(inventoryType + page, slot, equipSlot);
                return true;
            }
        }

        if (inventoryType != InventoryType.Inventory1) return FindAndEquip(item, InventoryType.Inventory1, equipSlot, ItemOrderModule.Instance()->InventorySorter);
        return false;
    }
    private unsafe void MoveItem(InventoryType sourceInventory, uint sourceSlot, uint equipSlot)
    {
        // from simpletweaks
        var sourceContainerId = GetContainerId(sourceInventory);
        var destinationContainerId = GetContainerId(InventoryType.EquippedItems);
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
