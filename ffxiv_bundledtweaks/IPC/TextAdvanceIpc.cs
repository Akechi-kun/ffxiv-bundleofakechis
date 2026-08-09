using Dalamud.Plugin.Ipc;

namespace ComplexTweaks.IPC;

public sealed class TextAdvanceIpc : BaseIPC, IPluginService {
    public int InitOrder => 10;

    public override Ipc Id => Ipc.TextAdvance;
    public override string Name => "TextAdvance";
    public override string Repo => Nightmare;

    private readonly ICallGateSubscriber<string, ExternalTerritoryConfig, bool> _enableExternalControl;
    private readonly ICallGateSubscriber<string, bool> _disableExternalControl;
    private readonly ICallGateSubscriber<bool> _isInExternalControl;

    public TextAdvanceIpc() {
        _enableExternalControl = Svc.Interface.GetIpcSubscriber<string, ExternalTerritoryConfig, bool>("TextAdvance.EnableExternalControl");
        _disableExternalControl = Svc.Interface.GetIpcSubscriber<string, bool>("TextAdvance.DisableExternalControl");
        _isInExternalControl = Svc.Interface.GetIpcSubscriber<bool>("TextAdvance.IsInExternalControl");
    }

    public bool EnableExternalControl(string pluginName, ExternalTerritoryConfig config)
        => _enableExternalControl.HasFunction && _enableExternalControl.InvokeFunc(pluginName, config);

    public bool DisableExternalControl(string pluginName)
        => _disableExternalControl.HasFunction && _disableExternalControl.InvokeFunc(pluginName);

    public bool IsInExternalControl()
        => _isInExternalControl.HasFunction && _isInExternalControl.InvokeFunc();

    public sealed class ExternalTerritoryConfig {
        public bool? EnableQuestAccept;
        public bool? EnableQuestComplete;
        public bool? EnableRewardPick;
        public bool? EnableRequestHandin;
        public bool? EnableCutsceneEsc;
        public bool? EnableCutsceneSkipConfirm;
        public bool? EnableTalkSkip;
        public bool? EnableRequestFill;
        public bool? EnableAutoInteract;
    }
}
