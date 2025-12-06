using ComplexTweaks.Tasks;
using ECommons;
using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.Excel;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Task = System.Threading.Tasks.Task;

namespace ComplexTweaks.Tweaks;

[Tweak]
internal class AutoEquipXPBoosts : Tweak {
    public override string Name => "Auto Equip Exp Items";
    public override string Description => "Automatically equips any exp boosting item on level change.";

    public override void Enable() => Svc.ClientState.LevelChanged += CheckForLevelSync;
    public override void Disable() => Svc.ClientState.LevelChanged -= CheckForLevelSync;

    private readonly List<ExpItem> _expItems =
    [
        new ExpItem(14043, 30, 30), // Brand-new ring
        new ExpItem(16039, 50, 30), // Ala Mhigan earrings
        new ExpItem(24589, 70, 30), // Aetheryte earring
        new ExpItem(31393, 80, 10), // Bozjan earrings
        new ExpItem(33648, 80, 30), // Menphina's earring
        new ExpItem(41081, 90, 30), // Azeyma's earrings
        new ExpItem(44410, 60, 30), // Neophyte's ring
    ];

    private unsafe void CheckForLevelSync(uint classJobId, uint level) {
        var expItems = _expItems.GroupBy(x => x.GameData.Value.EquipSlotCategory.RowId)
            .Where(group => group.Any(x => level <= x.MaxLevel && Inventory.HasItem(x.GameData.RowId)))
            .Select(group => group.Where(x => level <= x.MaxLevel && Inventory.HasItem(x.GameData.RowId))
            .OrderByDescending(x => x.GameData.Value.LevelItem.RowId)
            .ThenByDescending(x => x.Percent)
            .First()).ToList();
        Service.Automation.Start(new EquipItems(expItems));
    }

    private readonly unsafe struct ExpItem(uint ItemId, int MaxLevel, int Percent) {
        public RowRef<Item> GameData { get; init; } = Item.GetRef(ItemId);
        public int MaxLevel { get; init; } = MaxLevel;
        public int Percent { get; init; } = Percent;
        public readonly ExcelRow* Row = Framework.Instance()->ExcelModuleInterface->ExdModule->GetRowBySheetIndexAndRowIndex(10, ItemId);

        public bool CanEquip(out RowRef<LogMessage> errorMsg) {
            var logMessageId = InventoryManager.CanEquip(GameData.RowId, (byte)Svc.PlayerState.Race.RowId, (byte)Svc.PlayerState.Sex, (ushort)Player.Level, (byte)Player.JobId, (byte)Player.GrandCompany, Player.PvPRank, Row);
            errorMsg = LogMessage.GetRef((uint)logMessageId);
            return logMessageId is 0;
        }
    }

    private sealed class EquipItems(List<ExpItem> expItems) : CommonTasks {
        protected override async Task Execute() {
            using var scope = BeginScope("EquipItems");
            await WaitUntil(() => Player.ReadyAndLoaded, "WaitForLoad");
            if (Player.TerritoryIntendedUse is not (TerritoryIntendedUseEnum.Dungeon or TerritoryIntendedUseEnum.Raid or TerritoryIntendedUseEnum.Raid_2 or TerritoryIntendedUseEnum.Alliance_Raid)) return;
            if (GetRow<ContentFinderCondition>(Player.CurrentCfc) is { ContentType.RowId: 28 }) return; // skip ults

            foreach (var expItem in expItems) {
                if (!expItem.CanEquip(out var errorMsg)) {
                    Log($"Can't equip [#{expItem.GameData.RowId}] {expItem.GameData.Value.Name}: {errorMsg.Value.Text}");
                    continue;
                }
                await WaitUntil(() => Player.ReadyAndLoaded, "WaitForNotBusy");
                await WaitUntil(() => Game.HasPermission([109, 134]), "WaitForPermission");
                Log($"Equipping [#{expItem.GameData.RowId}] {expItem.GameData.Value.Name}");
                PlayerEx.Equip(expItem.GameData.RowId);
            }
        }
    }
}
