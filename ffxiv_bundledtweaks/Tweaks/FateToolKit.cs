using ComplexTweaks.Tweaks;
using ComplexTweaks.TweakSystem.Events;
using ComplexTweaks.Tasks;
using Dalamud.Game.ClientState.Fates;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using System.Threading.Tasks;

namespace Automaton.Features;

public class FateToolKitConfig
{
    [IntConfig(DefaultValue = 900)] public int MaxDuration = 900;
    [IntConfig(DefaultValue = 120)] public int MinTimeRemaining = 120;
    [IntConfig(DefaultValue = 90)] public int MaxProgress = 90;

    public HashSet<uint> blacklist = [];
    public HashSet<uint> whitelist = [];
    public List<uint> zones = [];
}

[Tweak]
[Requires(Ipc.Navmesh | Ipc.BossMod)]
public class FateToolKit : Tweak<FateToolKitConfig>
{
    public override string Name => "Fate Tool Kit (Date With Destiny)";
    public override string Description => "Fate tracker with additional fate automations.";

    private const string _presetName = "CBT - DwD"; // TODO: need xan to add chocobo, sync, and pull size tweaks
    private const string _presetCompressed = "G+oeAORUXTtl2E83R2j3llZskAlfup84YIqPcvnvPv2pIBTinOdHYxILZYONhdvCixJcH" +
        "VjTXq1a4zVxrKdTh4l+ajwMK8CA6Tyc5nc6AkBljhYFYPKZYHC3RlI3HOwVBXDNCIARXa+Sulk4MF2IDTvyM+mu67A85ro1l41lFPD" +
        "4HDRr+jJ+lqgYBTyicyCGEdXSXsx6jwIwGvr+qhG5cPg7ux4UgOHAdDTs1shizX8AmE7swXyfK4ikDgXgcut6DiDmy0GWr3Ceqm3KP" +
        "AUi3vTr/MYDIqUIika/g0XGBVFTtV9NmVwd97Mo0D5E0+2mysa1ST515q9Nhqv0zC4U0xhYxG0d0YAXUBI767Po07M7WuUIq9ZOfKw" +
        "0yUAAlmUo/U4tT7dV2GDlZqEJK6pUqQqWXEXl8c4iYx0ria7p3qLH0QfLbfG4i5Vw2S6Plki/Su9b5rANRs30xTYygW439tvfhnpxD" +
        "0yZ8+66jen4XikeLDoZEXGS6JzYj8BVZobtit3hurKtlkzR5WgOkj7s0XpOVV5ir+x8X6txRe6F4W3vfGeDYDoC+J+O/gE=";
    private static readonly string _preset = _presetCompressed.FromBase64();

    [CommandHandler(["/dwd", "/vfate"], "Opens the FATE tracker")]
    private void OnCommand(string _, string __) => Window<DateWithDestinyWindow>()?.Toggle();

    [TweakEvent(TweakEvent.FateJoined, AutoEnable = false)]
    private void OnFateJoined(Type _, EventArgs args)
    {
        if (args is FateEventArgs { FateId: var id } && Service.Automation.CurrentTask is FateGrind task && id != task.NextFate?.FateId) return;

        if (Service.BossMod.GetActive() != _presetName)
        {
            if (Service.BossMod.Get(_presetName) is null)
                Service.BossMod.Create(_preset, true);
            else
                Service.BossMod.SetActive(_presetName);
        }

        // todo: pull size based on role
    }

    [TweakEvent(TweakEvent.FateLeft, AutoEnable = false)]
    private void OnFateLeft(Type _, EventArgs __)
    {
        Service.BossMod.ClearActive();
        Service.Automation.Start(new FateGrind(Config));
    }

    [TweakEvent(TweakEvent.Died, AutoEnable = false)]
    private void OnDeath(Type _, EventArgs __) => Service.Automation.Start(new FateGrind(Config));

    private sealed class FateGrind(FateToolKitConfig config) : CommonTasks
    {
        protected override async Task Execute()
        {
            await (State switch
            {
                FateState.Unconscious => Revive(),
                FateState.Moving => MoveToFate(),
                FateState.WaitingForFates => WaitForFate(),
                _ => NextFrame(),
            });
        }

        public unsafe IFate? CurrentFate => Svc.Fates.CreateFateReference((nint)FateManager.Instance()->CurrentFate);
        public IFate? NextFate { get; set; }

        private const int MinTimeToPrioritise = 240; // logic: if there are two fates and the further one would time out before the closer one would finish, prioritise the further one
        public unsafe IOrderedEnumerable<IFate> AvailableFates => Svc.Fates.Where(FateConditions)
            .OrderByDescending(f => f.HasBonus && Player.Status.FirstOrDefault(x => DateWithDestiny.TwistOfFateStatusIDs.Contains(x.StatusId)) != null)
            .ThenByDescending(f => f.Progress)
            .ThenByDescending(f => f.HasBonus)
            .ThenBy(f => f.TimeRemaining < MinTimeToPrioritise)
            .ThenBy(f => Player.DistanceTo(f.Position));

        private bool FateConditions(IFate f)
            => f.Duration <= config.MaxDuration
            && f.Progress <= config.MaxProgress
            && (f.TimeRemaining < 0 || f.TimeRemaining > config.MinTimeRemaining)
            && !config.blacklist.Contains(f.FateId);

        private FateState State
        {
            get
            {
                if (Svc.Condition[ConditionFlag.Unconscious])
                    return FateState.Unconscious;

                if (CurrentFate is { })
                    return FateState.Engaging;

                if (CurrentFate is not { } && AvailableFates.FirstOrDefault() is { })
                    return FateState.Moving;

                if (!AvailableFates.Any())
                    return FateState.WaitingForFates;

                return FateState.Idle;
            }
        }

        private enum FateState
        {
            Idle,
            WaitingForFates,
            Moving,
            Engaging,
            Unconscious,
        }

        private async Task Revive()
        {
            using var scope = BeginScope("WaitingForRevive");
            if (Player.Revivable)
            {
                (var lastZone, var lastPos) = (Player.Territory, Player.Position);
                Service.Memory.ExecuteCommand?.Invoke((int)ExecuteCommandFlag.Revive, 8);
                await WaitUntilTerritory(Player.HomeAetheryteTerritory);
                await TeleportTo(lastZone, lastPos);
            }
            else await NextFrame();
        }

        private async Task MoveToFate()
        {
            if (AvailableFates.FirstOrDefault() is not { } nextFate) return;
            NextFate = nextFate;
            await WaitWhile(() => Player.IsBusy, "WaitingForNotBusy");
            await MoveTo(NextFate.Position.RandomPoint(NextFate.Radius * 0.5f).OnMesh(), MovementConfig.Everything);
        }

        private async Task WaitForFate()
        {
            Status = "Waiting for fates to spawn";
            await NextFrame(60);
        }
    }
}
