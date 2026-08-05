using Dalamud.Plugin.Ipc;

namespace ComplexTweaks.IPC;

[Ipc(Ipc.AutoRetainer)]
public sealed class AutoRetainerIPC : BaseIPC, IPluginService {
    public int InitOrder => 10;

    public override string Name => "AutoRetainer";
    public override string Repo => Punish;

    private readonly ICallGateSubscriber<bool> _isBusy;
    private readonly ICallGateSubscriber<bool> _getMultiModeEnabled;

    public AutoRetainerIPC() {
        _isBusy = Svc.Interface.GetIpcSubscriber<bool>("AutoRetainer.PluginState.IsBusy");
        _getMultiModeEnabled = Svc.Interface.GetIpcSubscriber<bool>("AutoRetainer.GetMultiModeEnabled");
    }

    public bool IsBusy() => _isBusy.HasFunction && _isBusy.InvokeFunc();
    public bool GetMultiModeEnabled() => _getMultiModeEnabled.HasFunction && _getMultiModeEnabled.InvokeFunc();
}
