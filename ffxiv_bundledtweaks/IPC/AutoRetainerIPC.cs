using Dalamud.Plugin.Ipc;

namespace ComplexTweaks.IPC;

public sealed class AutoRetainerIPC : BaseIPC, IPluginService, IDisposable {
    public override Ipc Id => Ipc.AutoRetainer;
    public override string Name => "AutoRetainer";
    public override string Repo => Punish;

    public sealed class OfflineCharacterData {
        public ulong CID;
        public string Name = "Unknown";
        public string World = "";
        public bool ExcludeRetainer;
        public bool ExcludeWorkshop;
    }

    private readonly string? _pluginName;
    private readonly ICallGateSubscriber<bool> _isBusy;
    private readonly ICallGateSubscriber<bool> _getMultiModeEnabled;
    private readonly ICallGateSubscriber<List<ulong>> _getRegisteredCIDs;
    private readonly ICallGateSubscriber<ulong, OfflineCharacterData> _getOfflineCharacterData;
    private readonly ICallGateSubscriber<object>? _onCharacterAdditionalTask;
    private readonly ICallGateSubscriber<string, object>? _onCharacterReadyForPostprocess;
    private readonly ICallGateSubscriber<string, object>? _requestCharacterPostProcess;
    private readonly ICallGateSubscriber<object>? _finishCharacterPostprocessRequest;

    public event Action? OnCharacterPostprocessStep;
    public event Action? OnCharacterReadyToPostProcess;

    public AutoRetainerIPC() : this(null) { }

    public AutoRetainerIPC(string? suffix) {
        _isBusy = Svc.Interface.GetIpcSubscriber<bool>("AutoRetainer.PluginState.IsBusy");
        _getMultiModeEnabled = Svc.Interface.GetIpcSubscriber<bool>("AutoRetainer.GetMultiModeEnabled");
        _getRegisteredCIDs = Svc.Interface.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs");
        _getOfflineCharacterData = Svc.Interface.GetIpcSubscriber<ulong, OfflineCharacterData>("AutoRetainer.GetOfflineCharacterData");

        if (suffix is null)
            return;

        _pluginName = Svc.Interface.InternalName + $"_{suffix}";
        _onCharacterAdditionalTask = Svc.Interface.GetIpcSubscriber<object>("AutoRetainer.OnCharacterAdditionalTask");
        _onCharacterReadyForPostprocess = Svc.Interface.GetIpcSubscriber<string, object>("AutoRetainer.OnCharacterReadyForPostprocess");
        _requestCharacterPostProcess = Svc.Interface.GetIpcSubscriber<string, object>("AutoRetainer.RequestCharacterPostprocess");
        _finishCharacterPostprocessRequest = Svc.Interface.GetIpcSubscriber<object>("AutoRetainer.FinishCharacterPostprocessRequest");

        _onCharacterAdditionalTask.Subscribe(OnCharacterAdditionalTaskHandler);
        _onCharacterReadyForPostprocess.Subscribe(OnCharacterReadyForPostprocessHandler);
    }

    public bool IsBusy() => _isBusy.HasFunction && _isBusy.InvokeFunc();
    public bool GetMultiModeEnabled() => _getMultiModeEnabled.HasFunction && _getMultiModeEnabled.InvokeFunc();

    public List<ulong> GetRegisteredCharacters()
        => _getRegisteredCIDs.HasFunction ? _getRegisteredCIDs.InvokeFunc() : [];

    public OfflineCharacterData? GetOfflineCharacterData(ulong cid)
        => _getOfflineCharacterData.HasFunction ? _getOfflineCharacterData.InvokeFunc(cid) : null;

    public void RequestCharacterPostprocess() {
        if (_requestCharacterPostProcess?.HasAction == true) {
            IPluginLog.Get().Debug($"[{_pluginName}] Requesting CharacterPostProcess");
            _requestCharacterPostProcess.InvokeAction(_pluginName!);
        }
        else
            IPluginLog.Get().Warning($"[{_pluginName}] Unable to request CharacterPostProcess");
    }

    public void FinishCharacterPostProcess() {
        if (_finishCharacterPostprocessRequest?.HasAction == true) {
            IPluginLog.Get().Debug($"[{_pluginName}] Finishing CharacterPostProcess");
            _finishCharacterPostprocessRequest.InvokeAction();
        }
        else
            IPluginLog.Get().Warning($"[{_pluginName}] Unable to finish CharacterPostProcess");
    }

    public void Dispose() {
        _onCharacterAdditionalTask?.Unsubscribe(OnCharacterAdditionalTaskHandler);
        _onCharacterReadyForPostprocess?.Unsubscribe(OnCharacterReadyForPostprocessHandler);
    }

    private void OnCharacterAdditionalTaskHandler() {
        try {
            OnCharacterPostprocessStep?.Invoke();
        }
        catch (Exception ex) {
            IPluginLog.Get().Error(ex, $"[{_pluginName}] {nameof(AutoRetainerIPC)}.{nameof(OnCharacterPostprocessStep)} failed.");
        }
    }

    private void OnCharacterReadyForPostprocessHandler(string plugin) {
        if (plugin != _pluginName)
            return;

        try {
            OnCharacterReadyToPostProcess?.Invoke();
        }
        catch (Exception ex) {
            IPluginLog.Get().Error(ex, $"[{_pluginName}] {nameof(AutoRetainerIPC)},{nameof(OnCharacterReadyToPostProcess)} failed.");
        }
    }
}
