using Dalamud.Game.Chat;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Extensions;

namespace ComplexTweaks.Tweaks;

[Tweak]
public class ReQueueCC : Tweak {
    public override string Name => "CC Error Requeue";
    public override string Description => "Requeues for Crystalline Conflict when your registration was cancelled due to a map change.";

    public override void Enable() => IChatGui.Get().LogMessage += CheckForMessage;
    public override void Disable() => IChatGui.Get().LogMessage -= CheckForMessage;

    private unsafe void CheckForMessage(ILogMessage message) {
        if (message.LogMessageId is 7392) {
            Log($"Requeueing for CC due to map change.");
            if (AgentContentsFinder.Instance()->SelectedContent.FirstOrNull(x => x.ContentType is ContentsType.Roulette) is { Id: (40 or 41) and var id }) {
                Log($"Requeueing for CC with id {id}.");
                ContentsFinder.Instance()->QueueInfo.QueueRoulette((byte)id);
            }
            else {
                Log($"Requeueing for casual CC. Unable to detect selected content.");
                ContentsFinder.Instance()->QueueInfo.QueueRoulette(40);
            }
        }
    }
}
