using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Ipc.Exceptions;
using System.Globalization;

namespace ComplexTweaks.Tweaks;

[Requires(Ipc.AutoRetainer)]
public class ARSwitcher : Tweak {
    public override string Name => "AutoRetainer Character Switcher";
    public override string Description => "Adds a DTR element and commands to switch to the prev/next character in AutoRetainer.";

    private IDtrBarEntry? _dtrBarEntry;

    public override void OnEnable() {
        _dtrBarEntry ??= IDtrBar.Get().Get("Character Index", "Unknown Character Index");
        _dtrBarEntry.OnClick = @event => {
            unsafe {
                var homeWorldId = IPlayerState.Get().HomeWorld.RowId;
                var currentWorldId = IPlayerState.Get().CurrentWorld.RowId;
                if (homeWorldId == currentWorldId) {
                    var target = FindCharacter(@event.ClickType is MouseClickType.Left ? 1 : -1);
                    SwitchCharacter(target);
                }
                else
                    ICommandManager.Get().ProcessCommand("/li");
            }
        };
        if (IClientState.Get().IsLoggedIn)
            UpdateDtrBar();
        IClientState.Get().Login += UpdateDtrBar;
    }

    public override void OnDisable() {
        _dtrBarEntry?.Remove();
        IClientState.Get().Login -= UpdateDtrBar;
    }

    private void UpdateDtrBar() {
        if (_dtrBarEntry?.UserHidden ?? true || !IPlayerState.Get().CurrentWorld.IsValid || !IPlayerState.Get().HomeWorld.IsValid)
            return;

        try {
            var currentWorld = IPlayerState.Get().CurrentWorld.Value.Name.ToString();
            var homeWorld = IPlayerState.Get().HomeWorld.Value.Name.ToString();
            var characterIds = AutoRetainerIPC.Get().GetRegisteredCharacters();
            var characterIdsOnHomeWorld = characterIds.Where(x => AutoRetainerIPC.Get().GetOfflineCharacterData(x)?.World == homeWorld).ToList();

            var seIconChar = SeIconChar.Instance1 + characterIdsOnHomeWorld.IndexOf(IPlayerState.Get().ContentId);
            if (currentWorld == homeWorld) {
                _dtrBarEntry.Text = seIconChar.ToIconString();

                var previous = FindCharacter(-1, showError: false);
                var next = FindCharacter(1, showError: false);
                if (previous != null && next != null)
                    _dtrBarEntry.Tooltip = $"Prev: {previous.ToString(homeWorld)}\nNext: {next.ToString(homeWorld)}";
                else if (previous != null)
                    _dtrBarEntry.Tooltip = $"Prev: {previous.ToString(homeWorld)}";
                else if (next != null)
                    _dtrBarEntry.Tooltip = $"Next: {next.ToString(homeWorld)}";
                else
                    _dtrBarEntry.Tooltip = null;
            }
            else {
                _dtrBarEntry.Text = $"{homeWorld} {seIconChar.ToIconString()}";
                _dtrBarEntry.Tooltip = $"Return to {homeWorld}";
            }

            if (!_dtrBarEntry.Shown)
                _dtrBarEntry.Shown = true;
        }
        catch (IpcError) {
            _dtrBarEntry.Shown = false;
        }
    }

    private Target? FindCharacter(int direction, bool showError = true) {
        try {
            Verbose($"Switching characters ({direction})");

            var characterIds = AutoRetainerIPC.Get().GetRegisteredCharacters();
            var index = characterIds.IndexOf(IPlayerState.Get().ContentId);
            if (index < 0) {
                if (showError)
                    ModuleMessage("Current character not known.");
                return null;
            }

            AutoRetainerIPC.OfflineCharacterData? target;
            do {
                index = (index + direction + characterIds.Count) % characterIds.Count;
                target = AutoRetainerIPC.Get().GetOfflineCharacterData(characterIds[index]);
                if (target?.CID == IPlayerState.Get().ContentId) {
                    if (showError)
                        ModuleMessage("No character to switch to found.");
                    return null;
                }

                if (target is { ExcludeRetainer: true, ExcludeWorkshop: true })
                    target = null;
            } while (target == null);

            return new Target(target.Name, target.World);
        }
        catch (IpcError) {
            ModuleMessage("Could not switch character, AutoRetainer API isn't available.");
            return null;
        }
    }

    [CommandHandler("/k+", "Switch to the next AR-enabled character.")]
    internal void NextCharacter(string command, string arguments) => SwitchCharacter(FindCharacter(1));

    [CommandHandler("/k-", "Switch to the previous AR-enabled character.")]
    internal void PreviousCharacter(string command, string arguments) => SwitchCharacter(FindCharacter(-1));

    [CommandHandler("/ks", $"Switch to a specific character,\n\t/ks [partial character name] - switch to the first character with a matching name.\n\t/ks [world name] [index] - switch to the Nth character on the specified world.")]
    internal void PickCharacter(string command, string arguments) {
        if (string.IsNullOrEmpty(arguments)) {
            ModuleMessage("Usage: /ks <world/name> [index]");
            return;
        }

        try {
            var args = arguments.Split(' ', 2);
            if (args.Length < 2 || !int.TryParse(args[1], CultureInfo.InvariantCulture, out var index))
                index = 1;

            var targets = AutoRetainerIPC.Get().GetRegisteredCharacters()
                .Select(characterId => AutoRetainerIPC.Get().GetOfflineCharacterData(characterId))
                .OfType<AutoRetainerIPC.OfflineCharacterData>()
                .Where(x => !x.ExcludeRetainer || !x.ExcludeWorkshop)
                .Select(x => new { x.Name, x.World })
                .ToList();

            var target = targets.Where(x => x.World.StartsWith(args[0], StringComparison.OrdinalIgnoreCase)).Skip(index - 1).FirstOrDefault() ?? targets.FirstOrDefault(x => x.Name.Contains(arguments, StringComparison.OrdinalIgnoreCase));
            if (target == null) {
                ModuleMessage($"No character found on world {args[0]} with #{index}.");
                return;
            }

            SwitchCharacter(new Target(target.Name, target.World));
        }
        catch (IpcError) {
            ModuleMessage("Could not switch character, AutoRetainer API isn't available.");
        }
    }

    private void SwitchCharacter(Target? target) {
        if (target == null)
            return;

        if (ICondition.Get()[ConditionFlag.BoundByDuty] || ICondition.Get()[ConditionFlag.BoundByDuty56] ||
            ICondition.Get()[ConditionFlag.BoundByDuty95] || ICondition.Get()[ConditionFlag.InDutyQueue] ||
            ICondition.Get()[ConditionFlag.Occupied] || ICondition.Get()[ConditionFlag.Occupied30] ||
            ICondition.Get()[ConditionFlag.Occupied33] || ICondition.Get()[ConditionFlag.Occupied38] ||
            ICondition.Get()[ConditionFlag.Occupied39] || ICondition.Get()[ConditionFlag.OccupiedInEvent] ||
            ICondition.Get()[ConditionFlag.OccupiedSummoningBell] || ICondition.Get()[ConditionFlag.OccupiedInQuestEvent] ||
            ICondition.Get()[ConditionFlag.OccupiedInCutSceneEvent] || ICondition.Get()[ConditionFlag.WatchingCutscene] ||
            ICondition.Get()[ConditionFlag.WatchingCutscene78] || ICondition.Get()[ConditionFlag.InCombat]) {
            INotificationManager.Get().AddNotification(new Notification {
                Title = $"{Svc.Interface.Manifest.Name} - {Name}",
                Content = "Can't switch characters (bound by duty or occupied)",
                Type = NotificationType.Error
            });
            return;
        }

        INotificationManager.Get().AddNotification(new Notification {
            Title = $"{Svc.Interface.Manifest.Name} - {Name}",
            Content = $"Switch to {target}.",
            Type = NotificationType.Success,
        });
        ICommandManager.Get().ProcessCommand($"/ays relog {target}");
    }

    private sealed record Target(string Name, string World) {
        public override string ToString() => $"{Name}@{World}";
        public string ToString(string? currentWorld) => currentWorld != World ? ToString() : Name;
    }
}
