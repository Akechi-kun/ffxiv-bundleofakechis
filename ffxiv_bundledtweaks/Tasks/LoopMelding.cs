using Dalamud.Game.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Threading.Tasks;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace ComplexTweaks.Tasks;

public sealed partial class LoopMelding(GameInventoryItem item) : CommonTasks
{
    private static readonly uint GettingTooAttachedVII = 1905;
    private static unsafe bool AgentActive => AgentMateriaAttach.Instance()->IsAgentActive();
    private static unsafe bool AgentLoading => AgentMateriaAttach.Instance()->UpdateState != 0;

    // if item.AmountMelded >= item.MateriaSlotCount, then forbid
    protected override async Task Execute()
    {
        Status = $"Getting Achievement Progress";
        var (_, current, max) = await GetAchievementProgress(GettingTooAttachedVII);
        try
        {
            while (current < max)
            {
                Status = $"Melding [{current}/{max}]";
                await Meld();

                Status = $"Retrieving [{current}/{max}]";
                unsafe { EventFramework.Instance()->MaterializeItem((InventoryItem*)item.Address, MaterializeEntryId.Retrieve); }
                await WaitUntilThenFalse(() => Svc.Condition[ConditionFlag.Occupied39], "Retrieving");
                current++;
            }
        }
        finally
        {
            unsafe { AgentMateriaAttach.Instance()->Hide(); }
        }
    }

    private async Task<(uint id, uint current, uint max)> GetAchievementProgress(uint achievementId)
    {
        using var scope = BeginScope($"WaitingOn#{achievementId}");
        unsafe { Achievement.Instance()->RequestAchievementProgress(achievementId); }
        return await WaitForReceiveAchievementProgress(id: achievementId);
    }

    [AddressHook<Achievement>(nameof(Achievement.MemberFunctionPointers.ReceiveAchievementProgress))]
    private unsafe void ReceiveAchievementProgress(Achievement* achievement, uint id, uint current, uint max)
        => ReceiveAchievementProgressHook.Original(achievement, id, current, max);

    private async Task Meld()
    {
        await Open();
        await SelectItem();
        if (GetUsableMateria() is not { } materia)
            Error($"No materia that can be guaranteed melded to {item.GameData.Value.Name}");
        //await SelectMateria(materia);
        await WaitUntilThenFalse(() => Svc.Condition[ConditionFlag.MeldingMateria], "Melding");
        // materiaattachdialog
        // selectyesno
    }

    private async Task Open()
    {
        if (AgentActive) return;
        bool res;
        unsafe { res = ActionManager.Instance()->UseAction(ActionType.GeneralAction, 13); }
        ErrorIf(!res, "Unable to open melding addon");
        await WaitUntil(() => AgentActive, $"WaitForAgent");
    }

    private async Task SelectItem()
    {
        var category = GetCategory(item);
        ErrorIf(category is AgentMateriaAttach.FilterCategory.None, $"{item.GameData.Value.Name} has no inventory category");

        unsafe
        {
            if (AgentMateriaAttach.Instance()->Category != category)
                ReceiveEvent(0, [0, (int)category]);
        }

        await WaitWhile(() => AgentLoading, "WaitAgentLoad");

        unsafe
        {
            var agent = AgentMateriaAttach.Instance();
            var it = item.ToPtr();
            for (var i = 0; i < agent->ItemCount; i++)
            {
                if (it == agent->Data->ItemsSorted[i].Value->Item)
                {
                    ReceiveEvent(0, [1, i, 1, 0]);
                    return;
                }
            }

            throw new KeyNotFoundException($"{item.GameData.Value.Name} not found");
        }
    }

    private async Task SelectMateria(uint id)
    {
        await WaitWhile(() => AgentLoading, "WaitAgentLoad");
        unsafe
        {
            var agent = AgentMateriaAttach.Instance();
            for (var i = 0; i < agent->MateriaCount; i++)
            {
                var invItem = agent->Data->MateriaSorted[i].Value->Item;
                if (invItem->ItemId == id)
                {
                    ReceiveEvent(0, [2, i, 1, 0]);
                    return;
                }
            }

            throw new KeyNotFoundException($"Materia #{id} not found");
        }
    }

    private unsafe void ReceiveEvent(ulong eventKind, int[] values)
    {
        var ret = new AtkValue();
        var atkvalues = stackalloc AtkValue[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            atkvalues[i].Type = ValueType.Int;
            atkvalues[i].Int = values[i];
        }
        AgentMateriaAttach.Instance()->ReceiveEvent(&ret, atkvalues, (uint)values.Length, eventKind);
    }

    private AgentMateriaAttach.FilterCategory GetCategory(GameInventoryItem item)
    {
        unsafe
        {
            return (InventoryType)item.ContainerType switch
            {
                InventoryType.Inventory1 or InventoryType.Inventory2 or InventoryType.Inventory3 or InventoryType.Inventory4 => AgentMateriaAttach.FilterCategory.Inventory,
                InventoryType.ArmoryMainHand or InventoryType.ArmoryOffHand => AgentMateriaAttach.FilterCategory.ArmouryWeapon,
                InventoryType.ArmoryHead or InventoryType.ArmoryBody or InventoryType.ArmoryHands => AgentMateriaAttach.FilterCategory.ArmouryHeadBodyHands,
                InventoryType.ArmoryLegs or InventoryType.ArmoryFeets => AgentMateriaAttach.FilterCategory.ArmouryLegsFeet,
                InventoryType.ArmoryEar or InventoryType.ArmoryNeck => AgentMateriaAttach.FilterCategory.ArmouryNeckEars,
                InventoryType.ArmoryWrist or InventoryType.ArmoryRings => AgentMateriaAttach.FilterCategory.ArmouryWristRing,
                InventoryType.EquippedItems => AgentMateriaAttach.FilterCategory.Equipped,
                _ => AgentMateriaAttach.FilterCategory.None
            };
        }
    }

    private unsafe uint? GetUsableMateria()
    {
        var agent = AgentMateriaAttach.Instance();
        foreach (var materia in agent->Data->MateriaSorted)
        {

        }
        return null;
    }
}
