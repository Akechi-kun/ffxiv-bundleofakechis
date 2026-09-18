using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace ComplexTweaks.Tweaks;

public unsafe partial class CollectableRewardTooltip : Tweak {
    public override string Name => "Collectable Reward Tooltip";
    public override string Description => "Shows the scrip rewards next to the collectability rating on item tooltips.";

    private InventoryItem _hoveredItem;

    // TODO: replace with CS
    [SigHook("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 89 9C 24 ?? ?? ?? ?? 4C 89 A4 24")]
    internal byte OnItemHovered(AgentItemDetail* thisPtr, InventoryItem** outItem, InventoryType* inventoryType, short* slot, uint itemId, uint index, InventoryItem* item) {
        var ret = OnItemHoveredHook.Original(thisPtr, outItem, inventoryType, slot, itemId, index, item);
        _hoveredItem = *item;
        return ret;
    }

    [SigHook("E8 ?? ?? ?? ?? 48 8B 45 ?? 48 8B 8F ?? ?? ?? ?? F7 40 ?? ?? ?? ?? ?? 75 ?? 33 D2 E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? B2 ?? E8")]
    internal void GenerateItemTooltip(AddonItemDetail* thisPtr, NumberArrayData* numberArray, StringArrayData* stringArray) {
        try {
            TryAddTooltip(stringArray);
        }
        catch (Exception ex) {
            Error(ex, "Error generating collectable reward tooltip");
        }
        GenerateItemTooltipHook.Original(thisPtr, numberArray, stringArray);
    }

    private void TryAddTooltip(StringArrayData* stringArray) {
        if (_hoveredItem.Handle is not { IsCollectible: true, CollectabilityReward: { } reward })
            return;
        if (stringArray->Span.Length <= StringArrayField.CollectabilityRating.Value || !stringArray->Span[(int)StringArrayField.CollectabilityRating].HasValue)
            return;

        var originalText = new ReadOnlySeStringSpan(stringArray->Span[(int)StringArrayField.CollectabilityRating]);
        var valueText = $" ({reward.Amount}x {reward.RewardItem.Name})";
        if (originalText.ExtractText().Contains(valueText))
            return;

        var builder = new SeStringBuilder().Append(originalText).Append(valueText);
        stringArray->SetValue(StringArrayField.CollectabilityRating.Value, builder.ToReadOnlySeString(), false);
    }

    private enum StringArrayField : int {
        ItemName = 0,
        ItemCategory = 2,
        RecastString = 6,
        RecastDuration = 9,
        Description = 13,
        Quantity = 14,
        EffectsString = 15,
        EffectsDescription = 16,
        CollectableHeader = 17,
        CollectabilityString = 18,
        CollectabilityRating = 19,
        SellText = 25, // sells for x / unsellable / market prohibited
    }
}
