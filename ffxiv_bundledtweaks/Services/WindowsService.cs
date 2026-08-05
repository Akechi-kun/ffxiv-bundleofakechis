using ComplexTweaks.UI;
using Dalamud.Interface.Windowing;

namespace ComplexTweaks.Services;

public sealed class WindowsService : IPluginService, IDisposable {
    public int InitOrder => 5;

    public WindowSystem WindowSystem { get; } = new(nameof(ComplexTweaks));

    private HaselWindow? _mainWindow;
    private DebugWindow? _debugWindow;

    public WindowsService() {
        GetMainWindow();
        GetDebugWindow();

        Svc.Interface.UiBuilder.Draw += Draw;
        Svc.Interface.UiBuilder.OpenMainUi += ToggleMain;
    }

    public void ToggleMain() => GetMainWindow().Toggle();

    public void ToggleDebug() => GetDebugWindow().Toggle();

    public void AddWindow(Window window) {
        if (!WindowSystem.Windows.Contains(window))
            WindowSystem.AddWindow(window);
    }

    public void RemoveWindow(Window window) {
        if (WindowSystem.Windows.Contains(window))
            WindowSystem.RemoveWindow(window);
    }

    public void RemoveWindow<T>() where T : Window {
        if (WindowSystem.GetWindow<T>() is { } window)
            RemoveWindow(window);
    }

    public T? GetWindow<T>() where T : Window => WindowSystem.GetWindow<T>() as T;

    public void Dispose() {
        Svc.Interface.UiBuilder.Draw -= Draw;
        Svc.Interface.UiBuilder.OpenMainUi -= ToggleMain;
        WindowSystem.RemoveAllWindows();
        _mainWindow = null;
        _debugWindow = null;
    }

    private void Draw() => WindowSystem.Draw();

    private HaselWindow GetMainWindow() {
        if (_mainWindow != null)
            return _mainWindow;
        _mainWindow = new HaselWindow();
        WindowSystem.AddWindow(_mainWindow);
        return _mainWindow;
    }

    private DebugWindow GetDebugWindow() {
        if (_debugWindow != null)
            return _debugWindow;
        _debugWindow = new DebugWindow();
        WindowSystem.AddWindow(_debugWindow);
        return _debugWindow;
    }
}
