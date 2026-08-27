namespace ComplexTweaks.Tweaks;

public partial class CommandsConfiguration {
    [BoolConfig(Label = "/equip")]
    public bool EnableEquip = false;
}

public partial class Commands : Tweak<CommandsConfiguration> {
    [CommandHandler("/equip", "Equip an item by ID", nameof(Config.EnableEquip))]
    internal void OnCommmandEquip(string _, string arguments) {
        if (!uint.TryParse(arguments, out var itemId)) return;
        var item = new ItemHandle(itemId);
        if (!item.TrySetItemLocation()) {
            ModuleMessage($"Failed to find item {itemId} in inventory");
            return;
        }
        if (item.CanEquip(out var logMessage))
            item.Equip();
        else
            ModuleMessage($"Unable to equip item {item}: {logMessage.Value.Text}");
    }
}

