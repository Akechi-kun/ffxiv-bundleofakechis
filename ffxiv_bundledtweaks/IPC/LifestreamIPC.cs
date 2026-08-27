using Dalamud.Plugin.Ipc;

namespace ComplexTweaks.IPC;

public sealed class LifestreamIPC : BaseIPC, IPluginService {
    public override Ipc Id => Ipc.Lifestream;
    public override string Name => "Lifestream";
    public override string Repo => Nightmare;

    private readonly ICallGateSubscriber<bool> _isBusy;
    private readonly ICallGateSubscriber<string, object> _executeCommand;

    public LifestreamIPC() {
        _isBusy = Svc.Interface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        _executeCommand = Svc.Interface.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand");
    }

    public bool IsBusy() => _isBusy.HasFunction && _isBusy.InvokeFunc();

    public void ExecuteCommand(string command) {
        if (_executeCommand.HasAction)
            _executeCommand.InvokeAction(command);
    }
}
