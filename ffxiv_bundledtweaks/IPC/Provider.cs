using Dalamud.Plugin.Ipc;

namespace ComplexTweaks.IPC;

public sealed class Provider : IPluginService, IDisposable {
    private readonly ICallGateProvider<string, bool> _isTweakEnabled;
    private readonly ICallGateProvider<string, bool, object> _setTweakState;

    public Provider() {
        _isTweakEnabled = Svc.Interface.GetIpcProvider<string, bool>("Automaton.IsTweakEnabled");
        _isTweakEnabled.RegisterFunc(IsTweakEnabled);

        _setTweakState = Svc.Interface.GetIpcProvider<string, bool, object>("Automaton.SetTweakState");
        _setTweakState.RegisterAction(SetTweakState);
    }

    public bool IsTweakEnabled(string className) => ConfigService.Get().Config.EnabledTweaks.Contains(className);

    public void SetTweakState(string className, bool state) {
        if (state)
            ConfigService.Get().Config.EnabledTweaks.Add(className);
        else
            ConfigService.Get().Config.EnabledTweaks.Remove(className);
    }

    public void Dispose() {
        _isTweakEnabled.UnregisterFunc();
        _setTweakState.UnregisterAction();
    }
}
