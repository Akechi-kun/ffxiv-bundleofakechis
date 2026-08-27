using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ComplexTweaks.Tweaks;

public class AutoInviteConfiguration {
    [StringConfig(IsRegex = nameof(IsRegex))] public string Pattern = string.Empty;
    [BoolConfig] public bool IsRegex = false;
    [BoolConfig] public bool TurnOffOnceFull = true;
    [IntConfig] public int DelayMs = 250;
    [ChatChannelConfig(Mode = ChatChannelConfigAttribute.ChatChannelMode.PlayerChat)] public List<XivChatType> Channels = [];
}

public partial class AutoInvite : Tweak<AutoInviteConfiguration> {
    // Based on https://github.com/Bluefissure/Inviter but without all the hooks
    public override string Name => "Auto Inviter";
    public override string Description => "Auto invite people to your party based on a chat message.";

    private bool On {
        get;
        set {
            IToastGui.Get().ShowNormal($"Auto Inviter {(value ? "enabled" : "disabled")}");
            _attempts = 0;
            field = value;
        }
    }
    private int _attempts = 0;

    [AddressHook<RaptureLogModule>(nameof(RaptureLogModule.MemberFunctionPointers.AddMsgSourceEntry))]
    private unsafe void AddMsgSourceEntry(RaptureLogModule* thisPtr, ulong contentId, ulong accountId, int messageIndex, ushort worldId, ushort chatType) {
        AddMsgSourceEntryHook.Original(thisPtr, contentId, accountId, messageIndex, worldId, chatType);

        if (!On) return;

        if (Config.Pattern.IsEmpty) {
            Log("Skipping invite: no pattern.");
            return;
        }

        if (!Config.Channels.Contains((XivChatType)chatType)) {
            Log("Skipping invite: not in valid chat channel.");
            return;
        }

        if (!RaptureLogModule.Instance()->GetLogMessageDetail(messageIndex, out var sender, out var rawMessage, out _, out _, out _, out _)) {
            Log("Skipping invite: unable to get message detail.");
            return;
        }

        if (!InfoProxyPartyInvite.CanInviteToParty(contentId, out var reason)) {
            Log($"Unable to invite to party. {reason}");
            if (reason is InfoProxyPartyInviteExtensions.FailedInviteReason.GroupFull && Config.TurnOffOnceFull) On = false;
            return;
        }

        var message = SeString.Parse(rawMessage.AsSpan()).TextValue;
        var matches = false;

        if (Config.IsRegex) {
            try {
                matches = Regex.Match(message, Config.Pattern, RegexOptions.IgnoreCase).Success;
            }
            catch (Exception ex) {
                Warning(ex, "Skipping invite: invalid regex pattern.");
                return;
            }
        }
        else
            matches = message.Contains(Config.Pattern, StringComparison.OrdinalIgnoreCase);

        if (matches) {
            if (SeString.Parse(sender.AsSpan()).Payloads.FirstOrDefault(p => p is PlayerPayload) is PlayerPayload playerPayload) {
                Log($"Attempting to invite {playerPayload.PlayerName}");
                StartInvite(contentId, playerPayload.PlayerName, (ushort)playerPayload.World.RowId);
                if (_attempts > 0) {
                    _attempts--;
                    Log($"Invites remaining: {_attempts}");
                    if (_attempts == 0)
                        On = false;
                }
            }
        }
    }

    private void StartInvite(ulong contentId, string playerName, ushort worldId) {
        Automation.Start(AutoTask.From(async t => {
            await t.DelayMs(Config.DelayMs);
            InfoProxyPartyInvite.Invite(contentId, playerName, worldId);
        }, name: "AutoInvite"));
    }

    [CommandHandler("/cinvite", "Toggle Auto Inviter", subCommandStrings: ["[0-9]s|Enable for specified seconds", "[0-9]a|Enable for specified number of invites"])]
    private void MainCommand(string command, string arguments) {
        if (string.IsNullOrEmpty(arguments)) {
            On ^= true;
            return;
        }

        if (arguments.EndsWith("a", StringComparison.OrdinalIgnoreCase) && int.TryParse(arguments[..^1], out var attempts)) {
            On = true;
            _attempts = attempts;
            Log($"Enabled for {attempts} invites.");
            return;
        }

        if (arguments.EndsWith("s", StringComparison.OrdinalIgnoreCase) && int.TryParse(arguments[..^1], out var seconds)) {
            On = true;
            Log($"Enabled for {seconds} seconds.");
            Task.Run(async () => {
                await Task.Delay(seconds * 1000);
                On = false;
            });
            return;
        }
    }
}
