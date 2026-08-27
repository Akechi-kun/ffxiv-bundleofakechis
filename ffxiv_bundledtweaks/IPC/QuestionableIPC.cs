using Dalamud.Plugin.Ipc;

namespace ComplexTweaks.IPC;

public sealed class QuestionableIPC : BaseIPC, IPluginService {
    public override Ipc Id => Ipc.Questionable;
    public override string Name => "Questionable";
    public override string Repo => Punish;

    private readonly ICallGateSubscriber<List<string>> _getCurrentlyActiveEventQuests;
    private readonly ICallGateSubscriber<string, bool> _startSingleQuest;

    public QuestionableIPC() {
        _getCurrentlyActiveEventQuests = Svc.Interface.GetIpcSubscriber<List<string>>("Questionable.GetCurrentlyActiveEventQuests");
        _startSingleQuest = Svc.Interface.GetIpcSubscriber<string, bool>("Questionable.StartSingleQuest");
    }

    public List<string> GetCurrentlyActiveEventQuests()
        => _getCurrentlyActiveEventQuests.HasFunction ? _getCurrentlyActiveEventQuests.InvokeFunc() : [];

    public bool StartSingleQuest(string questId)
        => _startSingleQuest.HasFunction && _startSingleQuest.InvokeFunc(questId);
}
