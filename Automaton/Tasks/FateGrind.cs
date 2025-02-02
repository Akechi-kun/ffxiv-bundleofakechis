using Automaton.Features;
using Dalamud.Game.ClientState.Fates;
using ECommons.Automation;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System.Threading.Tasks;
using FateState = Dalamud.Game.ClientState.Fates.FateState;

namespace Automaton.Tasks;
public sealed class FateGrind(DateWithDestinyConfiguration config) : CommonTasks
{
    // TODO:
    // auto detect yokai event, set yokai mode accordingly
    private unsafe bool InFate => FateManager.Instance()->CurrentFate != null;

    private static Vector3 TargetPos;
    private ushort nextFateId;
    private unsafe bool WithinNextFate => Svc.Fates.FirstOrDefault(f => f.FateId == nextFateId) is { } fate && Player.DistanceTo(fate.Position) < fate.Radius;
    private unsafe bool NextFateInPrep => Svc.Fates.FirstOrDefault(f => f.FateId == nextFateId) is { } fate && fate.State == FateState.Preparation;
    private byte fateMaxLevel;
    //public unsafe IOrderedEnumerable<IFate> AvailableFates => Svc.Fates.Where(FateConditions).OrderByDescending(f => f.Progress).ThenByDescending(f => f.HasBonus).ThenBy(f => Player.DistanceTo(f.Position));
    public unsafe IOrderedEnumerable<IFate> AvailableFates => Svc.Fates.Where(FateConditions).OrderBy(f => Player.DistanceTo(f.Position));

    private const uint ChocoboMinTime = 300; // seconds
    private const uint ChocoboSummonItemId = 4868;
    private unsafe float ChocoboTimeLeft => UIState.Instance()->Buddy.CompanionInfo.TimeLeft;
    private bool CanSummonChocobo
        => !Player.IsBusy
        && Player.TerritoryIntendedUse == ECommons.ExcelServices.TerritoryIntendedUseEnum.Open_World
        && Inventory.HasItem(ChocoboSummonItemId)
        && !Game.IsActionInUse(ActionType.Item, ChocoboSummonItemId);

    private ushort FateID
    {
        get; set
        {
            if (field != value)
                SyncFate(value);
            field = value;
        }
    }

    protected override async Task Execute()
    {
        while (true)
        {
        start:
            if (Svc.Condition[ConditionFlag.Unconscious])
            {
                if (Player.IsDead) // TODO: if in a party, wait for res instead of reviving
                    await Resurrect();
                else
                    await NextFrame();
                goto start;
            }

            if (Svc.Condition[ConditionFlag.InCombat])
            {
                //if (ShouldLevelSyncCheese()) // this isn't helpful when the fate level is close to max
                //    await LevelSyncCheese();
                Status = "Waiting for combat to end";
                await NextFrame(30);
                goto start;
            }

            if (CanSummonChocobo && ChocoboTimeLeft <= ChocoboMinTime)
                await SummonChocobo();

            if (NextFateInPrep && WithinNextFate)
                await ActivateFate();

            if (InFate)
            {
                unsafe
                {
                    Status = "Syncing to Fate";
                    fateMaxLevel = FateManager.Instance()->CurrentFate->MaxLevel;
                    FateID = FateManager.Instance()->CurrentFate->FateId;
                    Service.BossMod.SetActive("AI");
                }
            }
            else
            {
                FateID = 0;
                // don't clear preset immediately in case we're still in combat after fate ends
                await WaitWhile(() => Player.IsBusy, "WaitingForNotBusy");
                Service.BossMod.ClearActive();
            }

            if (!InFate)
            {
                var nextFate = AvailableFates.FirstOrDefault();
                if (nextFate is not null)
                {
                    nextFateId = nextFate.FateId;
                    TargetPos = GetRandomPointInFate(nextFateId);
                    await WaitWhile(() => Player.IsBusy, "WaitingForNotBusy");
                    await MoveTo(TargetPos, 5, true, true);
                }
            }

            if (!AvailableFates.Any())
            {
                //if (config.SwapZones)
                //    await SwapZones();
                //else
                Status = "Waiting for fates to spawn";
                await NextFrame();
            }

            await NextFrame();
        }
    }

    private async Task SummonChocobo()
    {
        Status = "Summoning Chocobo";
        Game.UseItem(ChocoboSummonItemId);
        await WaitUntil(() => ChocoboTimeLeft > ChocoboMinTime, "WaitingForChocobo");
    }

    private async Task LevelSyncCheese()
    {
        Status = "About to die. Abusing level sync";
        SyncFate(FateID, true);
        await NextFrame(60 * 10);
        SyncFate(FateID);
    }

    private unsafe DGameObject? FateActivationNpc => Svc.Objects.FirstOrDefault(o => o.Struct()->NamePlateIconId == 60093);
    private async Task ActivateFate()
    {
        if (FateActivationNpc is { } npc)
        {
            Status = "Activating fate";
            await MoveTo(npc.Position, 3, dismount: true);
            unsafe bool FateRunning() => FateManager.Instance()->GetFateById(nextFateId)->State == FFXIVClientStructs.FFXIV.Client.Game.Fate.FateState.Running;
            await InteractWith(npc, () => FateRunning());
        }
    }

    private async Task Resurrect()
    {
        Status = "Reviving";
        Service.Memory.ExecuteCommand?.Invoke((int)ExecuteCommandFlag.Revive, 8); // TODO: it's either 8 or 5 depending on what GameMain.field_4095 is
        await WaitWhile(() => Player.Territory != Player.HomeAetheryteTerritory || Player.IsBusy, "WaitingForRevive");
    }

    private unsafe bool ShouldLevelSyncCheese()
    {
        if (Player.IsLevelSynced && Player.SyncedLevel != Player.UnsyncedLevel)
            if (Player.Character->Health / Player.Character->MaxHealth < 0.15f) return true;
        return false;
    }

    private async Task SwapZones()
    {
        // if we're achievement farming, find the next zone where the achievement isn't completed, otherwise, pick a random zone within the same expac
        // if we're yokai farming, find the next zone where the yokai isn't completed
        var zoneId = GetNextAchievementZone() is { } zone ? zone : GetRandomSameExpacZone();
        await TeleportTo(zoneId, default);
    }

    private unsafe uint? GetNextAchievementZone()
    {
        var agent = AgentFateProgress.Instance();
        if (agent == null) return null;
        // prioritise zones in the same expac as current area
        var currentTabIndex = Array.FindIndex(agent->Tabs.ToArray(), tab => tab.Zones.ToArray().Any(zone => Player.Territory == zone.TerritoryTypeId));

        if (currentTabIndex != -1 && currentTabIndex < agent->Tabs.Length - 1)
        {
            // get zone in expac that needs fates
            var nullableZone = agent->Tabs[currentTabIndex].Zones.ToArray().FirstOrNull(zone => zone.NeededFates - zone.FateProgress > 0);
            return nullableZone is { } zone ? zone.TerritoryTypeId : null;
        }
        else
        {
            // get zone from any shared fate expac that needs fates
            var nullableZone = agent->Tabs.ToArray().SelectMany(tab => tab.Zones.ToArray()).FirstOrNull(zone => zone.NeededFates - zone.FateProgress > 0);
            return nullableZone is { } zone ? zone.TerritoryTypeId : null;
        }
    }

    private uint GetRandomSameExpacZone()
    {
        var rows = FindRows<TerritoryType>(x => x.ExVersion.RowId == GetRow<TerritoryType>(Player.Territory)!.Value.ExVersion.RowId);
        return rows[new Random().Next(rows.Length)].RowId;
    }

    private bool FateConditions(IFate f) => f.Duration <= config.MaxDuration && f.Progress <= config.MaxProgress && (f.TimeRemaining < 0 || f.TimeRemaining > config.MinTimeRemaining) && !config.blacklist.Contains(f.FateId);

    public IOrderedEnumerable<IFate> GetFates() => Svc.Fates.Where(FateConditions)
        .OrderByDescending(x => config.PrioritizeBonusFates && x.HasBonus && (!config.BonusWhenTwist || Player.Status.FirstOrDefault(x => DateWithDestiny.TwistOfFateStatusIDs.Contains(x.StatusId)) != null))
        .ThenByDescending(x => config.PrioritizeStartedFates && x.Progress > 0)
        .ThenBy(f => Vector3.Distance(PlayerEx.Position, f.Position));

    private unsafe Vector3 GetRandomPointInFate(ushort fateID)
    {
        var fate = FateManager.Instance()->GetFateById(fateID);
        var angle = new Random().NextDouble() * 2 * Math.PI;
        // Get a random point in a circle within half its radius
        var randomPoint = new Vector3((float)(fate->Location.X + fate->Radius / 2 * Math.Cos(angle)), fate->Location.Y, (float)(fate->Location.Z + fate->Radius / 2 * Math.Sin(angle)));
        var point = Service.Navmesh.NearestPoint(randomPoint, 5, 5);
        return (Vector3)(point == null ? fate->Location : point);
    }

    private unsafe void SyncFate(ushort value, bool unsync = false)
    {
        if (value != 0 && !Player.IsLevelSynced)
        {
            if (Player.Level > fateMaxLevel)
                Chat.Instance.SendMessage("/lsync");
        }
        if (unsync && Player.IsLevelSynced)
            Chat.Instance.SendMessage("/lsync");
    }
}
