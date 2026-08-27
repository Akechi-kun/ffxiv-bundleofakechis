using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Hooking.Internal.Verification;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility.Signatures;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ComplexTweaks.TweakSystem;

public abstract partial class Tweak : ITweak {
    public Tweak() {
        CachedType = GetType();
        InternalName = CachedType.Name;
        IncompatibilityWarnings = [.. CachedType.GetCustomAttributes<IncompatibilityWarningAttribute>()];

        Requirements = IPCRegistry.Get().GetMany([.. CachedType.GetCustomAttributes<RequiresAttribute>().SelectMany(r => r.Id.Flags).Where(id => id != Ipc.None).Distinct()]);
        RequiredClientStructsVersion = (CachedType.GetCustomAttribute<RequiresClientStructsAttribute>()?.MinVersion ?? 0, CachedType.GetCustomAttribute<RequiresClientStructsAttribute>()?.MaxVersion ?? uint.MaxValue);
        IsDebug = CachedType.GetCustomAttribute<DebugAttribute>() != null;
        var disabledAttr = CachedType.GetCustomAttribute<DisabledAttribute>();
        Disabled = disabledAttr != null;
        DisabledReason = disabledAttr?.Reason;

        try {
            SetupHooks();
            Automation = new();
        }
        catch (SignatureException ex) {
            Error(ex, $"{nameof(SignatureException)}, flagging as outdated");
            Status = TweakStatus.Outdated;
        }
        catch (HookVerificationException ex) {
            Error(ex, $"{nameof(HookVerificationException)}, flagging as outdated");
            Status = TweakStatus.Outdated;
        }
        catch (Exception ex) {
            Error(ex, "Unexpected error during setup");
            Status = TweakStatus.Error;
        }
    }

    public Type CachedType { get; }
    public string InternalName { get; }
    public IncompatibilityWarningAttribute[] IncompatibilityWarnings { get; }
    public BaseIPC[] Requirements { get; }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public bool IsDebug { get; }
    public bool Disabled { get; }
    public string? DisabledReason { get; }

    public TweakStatus Status { get; protected set; } = TweakStatus.Disabled;
    public (uint Min, uint Max) RequiredClientStructsVersion { get; }

    protected Automation Automation { get; private set; } = null!;
    internal void StopAutomation() => Automation.Stop();

    protected Type? CachedConfigType { get; set; }
    protected Type? CachedWindowType { get; set; }
    protected Window? _window;

    protected virtual object? GetConfigObject() => null;

    protected TWindow? Window<TWindow>() where TWindow : Window
        => _window as TWindow ?? WindowsService.Get().GetWindow<TWindow>();

    public virtual void SetupHooks() { }

    public virtual void OnEnable() { }
    public virtual void OnDisable() { }

    public bool ShouldEnable() => C.EnabledTweaks.Contains(InternalName) && Status != TweakStatus.Enabled && (!IsDebug || C.ShowDebug);
    public bool ShouldDisable() => Status == TweakStatus.Enabled && (!C.EnabledTweaks.Contains(InternalName) || IsDebug && !C.ShowDebug);

    public Task StartAsync(CancellationToken _) {
        if (!C.EnabledTweaks.Contains(InternalName))
            return Task.CompletedTask;

        if (IsDebug && !C.ShowDebug) {
            C.EnabledTweaks.Remove(InternalName);
            return Task.CompletedTask;
        }

        if (Status == TweakStatus.Enabled || Status.IsTerminal())
            return Task.CompletedTask;

        if (!CanBeEnabled()) {
            ModuleMessage(Requirements.Any(r => !r.IsLoaded)
                ? "Feature not enabled due to missing dependencies. Please install them then re-enable this feature."
                : $"Feature not enabled due to invalid ClientStructs version [{Svc.Interface.ClientStructsVersion}].");
            return Task.CompletedTask;
        }

        try {
            Information("Enabling tweak");
            EnsureWindow();
            EnableCommands();
            foreach (var hook in EnumerateHooks())
                EnableHook(hook);
            OnEnable();
            Status = TweakStatus.Enabled;
        }
        catch (SignatureException ex) {
            Status = TweakStatus.Outdated;
            Error(ex, "Error while enabling tweak");
            RemoveOwnedWindow();
        }
        catch (KeyNotFoundException ex) {
            Status = TweakStatus.Outdated;
            Error(ex, "Error while enabling tweak");
            RemoveOwnedWindow();
        }
        catch (Exception ex) {
            Status = TweakStatus.Error;
            Error(ex, "Error while enabling tweak");
            RemoveOwnedWindow();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken _) {
        if (Status != TweakStatus.Enabled)
            return Task.CompletedTask;

        try {
            Information("Disabling tweak");
            DisableCommands();
            foreach (var hook in EnumerateHooks())
                DisableHook(hook);
            OnDisable();
            Automation.Stop();
            RemoveOwnedWindow();
            Status = TweakStatus.Disabled;
        }
        catch (Exception ex) {
            Status = TweakStatus.Error;
            Error(ex, "Error while disabling tweak");
        }

        return Task.CompletedTask;
    }

    public virtual void Dispose() {
        if (Status is TweakStatus.Disposed or TweakStatus.Outdated)
            return;

        try {
            Information("Disposing tweak");
            if (Status == TweakStatus.Enabled) {
                DisableCommands();
                OnDisable();
                Automation?.Stop();
                RemoveOwnedWindow();
            }

            foreach (var hook in EnumerateHooks())
                DisposeHook(hook);

            Automation?.Dispose();
        }
        catch (Exception ex) {
            Error(ex, "Error while disposing tweak");
        }

        Status = TweakStatus.Disposed;
    }

    public bool CanBeEnabled()
        => Status is TweakStatus.Disabled && !Disabled && HasRuntimeRequirements();

    public bool HasRuntimeRequirements()
        => Requirements.All(r => r.IsLoaded) && MeetsClientStructsRequirements();

    public bool MeetsClientStructsRequirements()
        => P.IsLocalCs || Svc.Interface.ClientStructsVersion <= RequiredClientStructsVersion.Max && Svc.Interface.ClientStructsVersion >= RequiredClientStructsVersion.Min;

    private IEnumerable<object> EnumerateHooks()
        => CachedType
            .GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(prop => prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Hook<>))
            .Select(prop => prop.GetValue(this))
            .Where(hook => hook != null)!;

    private static void EnableHook(object hook) => InvokeHook(hook, nameof(Hook<>.Enable));
    private static void DisableHook(object hook) => InvokeHook(hook, nameof(Hook<>.Disable));
    private static void DisposeHook(object hook) => InvokeHook(hook, nameof(IDisposable.Dispose));

    private static void InvokeHook(object hook, string methodName)
        => hook.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)!.Invoke(hook, null);

    private void EnsureWindow() {
        if (CachedWindowType == null)
            return;

        var windows = WindowsService.Get();
        var existing = windows.WindowSystem.Windows.FirstOrDefault(w => w.GetType() == CachedWindowType);
        if (existing != null) {
            _window = (Window)existing;
            return;
        }

        // window was stale somehow
        if (_window != null && !windows.WindowSystem.Windows.Contains(_window))
            _window = null;

        if (_window == null) {
            var constructor = CachedWindowType.GetConstructor([CachedType]);
            if (constructor != null)
                _window = (Window?)constructor.Invoke([this]);
            else {
                constructor = CachedWindowType.GetConstructor([]);
                _window = constructor != null
                    ? (Window?)constructor.Invoke([])
                    : throw new InvalidOperationException($"Window type {CachedWindowType.Name} must have either a parameterless constructor or a constructor that takes {CachedType.Name}.");
            }
        }

        windows.AddWindow(_window!);
    }

    private void RemoveOwnedWindow() {
        if (_window == null)
            return;

        try {
            WindowsService.Get().RemoveWindow(_window);
        }
        catch (Exception ex) {
            Error(ex, $"Failed to remove window {CachedWindowType?.Name}");
        }
        finally {
            _window = null;
        }
    }
}

public abstract partial class Tweak // Config / Commands
{
    protected IEnumerable<MethodInfo> CommandHandlers
        => CachedType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(mi => mi.GetCustomAttribute<CommandHandlerAttribute>() != null);

    public virtual void DrawConfig() {
        var config = GetConfigObject();
        if (CachedConfigType != null && config != null) {
            var configFields = CachedConfigType.GetFields()
                .Select(fieldInfo => (FieldInfo: fieldInfo, Attribute: fieldInfo.GetCustomAttribute<BaseConfigAttribute>()))
                .Where(tuple => tuple.Attribute != null)
                .Cast<(FieldInfo, BaseConfigAttribute)>();

            if (configFields.Any()) {
                ImGui.DrawSection("Configuration");

                foreach (var (field, attr) in configFields) {
                    var hasDependency = !string.IsNullOrEmpty(attr.DependsOn);
                    var isDisabled = hasDependency && (bool?)CachedConfigType.GetField(attr.DependsOn)?.GetValue(config) == false;

                    using var id = ImRaii.PushId(field.Name);
                    using var indent = ImGui.ConfigIndent(hasDependency);
                    using var disabled = ImRaii.Disabled(isDisabled);

                    attr.Draw(this, config, field);
                }
            }
        }

        DrawCommands();
    }

    public virtual void OnConfigChange(string fieldName) { }

    internal void OnConfigChangeInternal(string fieldName) {
        foreach (var methodInfo in CommandHandlers) {
            var attr = methodInfo.GetCustomAttribute<CommandHandlerAttribute>()!;
            if (attr.ConfigFieldName != fieldName)
                continue;

            var enabled = string.IsNullOrEmpty(attr.ConfigFieldName);
            if (!string.IsNullOrEmpty(attr.ConfigFieldName) && CachedConfigType != null) {
                var config = GetConfigObject();
                if (config != null)
                    enabled |= (CachedConfigType.GetField(attr.ConfigFieldName)?.GetValue(config) as bool?)
                        ?? throw new InvalidOperationException($"Configuration field {attr.ConfigFieldName} in {CachedConfigType.Name} not found.");
            }

            if (enabled && methodInfo.GetCustomAttributes<RequiresAttribute>().SelectMany(r => r.Id.Flags).Where(id => id != Ipc.None).Distinct().ToArray() is { Length: > 0 } reqs) {
                if (!IPCRegistry.Get().AreAllLoaded(reqs)) {
                    var missing = IPCRegistry.Get().GetMissing(reqs);
                    Warning($"Cannot enable command(s) [{string.Join(", ", attr.Commands)}]: missing dependencies: {string.Join(", ", missing.Select(ipc => ipc.Name))}");
                    enabled = false;
                }
            }

            if (enabled)
                foreach (var c in attr.Commands)
                    EnableCommand(c, attr.HelpMessage, methodInfo);
            else
                foreach (var c in attr.Commands)
                    DisableCommand(c);
        }

        try {
            OnConfigChange(fieldName);
        }
        catch (Exception ex) {
            Error(ex, "Unexpected error during OnConfigChange");
            return;
        }

        ConfigService.Get().Save();
    }

    protected virtual void EnableCommands(bool onlyAbsent = false) {
        foreach (var methodInfo in CommandHandlers) {
            var attr = methodInfo.GetCustomAttribute<CommandHandlerAttribute>()!;
            var enabled = string.IsNullOrEmpty(attr.ConfigFieldName);

            if (!string.IsNullOrEmpty(attr.ConfigFieldName) && CachedConfigType != null) {
                var config = GetConfigObject();
                if (config != null)
                    enabled |= (CachedConfigType.GetField(attr.ConfigFieldName)?.GetValue(config) as bool?)
                        ?? throw new InvalidOperationException($"Configuration field {attr.ConfigFieldName} in {CachedConfigType.Name} not found.");
            }

            if (enabled && methodInfo.GetCustomAttributes<RequiresAttribute>().SelectMany(r => r.Id.Flags).Where(id => id != Ipc.None).Distinct().ToArray() is { Length: > 0 } reqs) {
                if (!IPCRegistry.Get().AreAllLoaded(reqs)) {
                    if (!onlyAbsent) {
                        var missing = IPCRegistry.Get().GetMissing(reqs);
                        var missingNames = missing.Length > 0 ? string.Join(", ", missing.Select(ipc => ipc.Name)) : "one or more required IPCs are not registered";
                        Warning($"Cannot enable command(s) [{string.Join(", ", attr.Commands)}]: missing dependencies: {missingNames}");
                    }
                    continue;
                }
            }

            if (!enabled)
                continue;

            foreach (var c in attr.Commands) {
                if (onlyAbsent && ICommandManager.Get().Commands.ContainsKey(c))
                    continue;
                EnableCommand(c, attr.HelpMessage, methodInfo);
            }
        }
    }

    internal void RefreshCommands() {
        if (Status != TweakStatus.Enabled || !HasRuntimeRequirements())
            return;
        try {
            EnableCommands(onlyAbsent: true);
        }
        catch (Exception ex) {
            Error(ex, "Unexpected error during RefreshCommands");
        }
    }

    protected virtual void DisableCommands() {
        foreach (var methodInfo in CommandHandlers) {
            var attr = methodInfo.GetCustomAttribute<CommandHandlerAttribute>()!;
            var enabled = string.IsNullOrEmpty(attr.ConfigFieldName);

            if (!string.IsNullOrEmpty(attr.ConfigFieldName) && CachedConfigType != null) {
                var config = GetConfigObject();
                if (config != null)
                    enabled |= (CachedConfigType.GetField(attr.ConfigFieldName)?.GetValue(config) as bool?)
                        ?? throw new InvalidOperationException($"Configuration field {attr.ConfigFieldName} in {CachedConfigType.Name} not found.");
            }

            if (enabled)
                foreach (var c in attr.Commands)
                    DisableCommand(c);
        }
    }

    protected void DrawCommands() {
        var commandHandlers = CommandHandlers
            .Select(m => m.GetCustomAttribute<CommandHandlerAttribute>()!)
            .Where(attr =>
                string.IsNullOrEmpty(attr.ConfigFieldName) ||
                CachedConfigType != null && GetConfigObject() != null && (bool?)CachedConfigType.GetField(attr.ConfigFieldName)?.GetValue(GetConfigObject()) == true)
            .Where(attr => attr.Commands.Any(cmd => ICommandManager.Get().Commands.ContainsKey(cmd)));

        if (!commandHandlers.Any())
            return;

        ImGui.DrawSection("Available Commands");
        foreach (var attr in commandHandlers) {
            foreach (var cmd in attr.Commands.Where(ICommandManager.Get().Commands.ContainsKey)) {
                var commandInfo = ICommandManager.Get().Commands[cmd];
                ImGui.Text($"{cmd}");
                if (!string.IsNullOrEmpty(commandInfo.HelpMessage)) {
                    ImGui.SameLine();
                    ImGui.TextColoredWrapped(Colors.Grey, commandInfo.HelpMessage);
                }

                foreach (var subCmd in attr.SubCommands) {
                    using var subIndent = ImGui.ConfigIndent();
                    ImGui.Text($"{cmd} {subCmd.Subcommand}");
                    ImGui.SameLine();
                    ImGui.TextColoredWrapped(Colors.Grey, subCmd.HelpMessage);
                }
            }
        }
    }

    private void EnableCommand(string command, string helpMessage, MethodInfo methodInfo) {
        var originalHandler = methodInfo.CreateDelegate<IReadOnlyCommandInfo.HandlerDelegate>(this);
        void handler(string cmd, string args) {
            if (methodInfo.GetCustomAttributes<RequiresAttribute>().SelectMany(r => r.Id.Flags).Where(id => id != Ipc.None).Distinct().ToArray() is { Length: > 0 } reqs) {
                if (!IPCRegistry.Get().AreAllLoaded(reqs)) {
                    var missing = IPCRegistry.Get().GetMissing(reqs);
                    ModuleMessage($"Command {cmd} requires: {string.Join(", ", missing.Select(ipc => ipc.Name))}");
                    return;
                }
            }

            originalHandler(cmd, args);
        }

        // replace if already registered
        if (ICommandManager.Get().Commands.ContainsKey(command))
            ICommandManager.Get().RemoveHandler(command);

        if (ICommandManager.Get().AddHandler(command, new CommandInfo(handler) { HelpMessage = helpMessage, DisplayOrder = 1 }))
            Log($"Added CommandHandler for {command}");
        else
            Warning($"Could not add CommandHandler for {command}");
    }

    private void DisableCommand(string command) {
        if (ICommandManager.Get().RemoveHandler(command))
            Log($"Removed CommandHandler for {command}");
        else
            Warning($"Could not remove CommandHandler for {command}");
    }
}

public abstract partial class Tweak // Logging
{
    public void Log(string messageTemplate)
        => Information(messageTemplate);
    public void Log(Exception exception, string messageTemplate)
        => Information(exception, messageTemplate);
    public void Verbose(string messageTemplate)
        => IPluginLog.Get().Verbose($"[{InternalName}] {messageTemplate}");
    public void Verbose(Exception exception, string messageTemplate)
        => exception.LogVerbose($"[{InternalName}] {messageTemplate}");
    public void Debug(string messageTemplate)
        => IPluginLog.Get().Debug($"[{InternalName}] {messageTemplate}");
    public void Debug(Exception exception, string messageTemplate)
        => exception.LogDebug($"[{InternalName}] {messageTemplate}");
    public void Information(string messageTemplate)
        => IPluginLog.Get().Information($"[{InternalName}] {messageTemplate}");
    public void Information(Exception exception, string messageTemplate)
        => exception.LogInfo($"[{InternalName}] {messageTemplate}");
    public void Warning(string messageTemplate)
        => IPluginLog.Get().Warning($"[{InternalName}] {messageTemplate}");
    public void Warning(Exception exception, string messageTemplate)
        => exception.LogWarning($"[{InternalName}] {messageTemplate}");
    public void Error(string messageTemplate)
        => IPluginLog.Get().Error($"[{InternalName}] {messageTemplate}");
    public void Error(Exception exception, string messageTemplate)
        => exception.Log($"[{InternalName}] {messageTemplate}");
    public void Fatal(string messageTemplate)
        => IPluginLog.Get().Fatal($"[{InternalName}] {messageTemplate}");
    public void Fatal(Exception exception, string messageTemplate)
        => exception.LogFatal($"[{InternalName}] {messageTemplate}");

    public void ModuleMessage(SeString messageTemplate) => ModuleMessage(messageTemplate.TextValue);
    public void ModuleMessage(string messageTemplate) {
        IChatGui.Get().Print(new XivChatEntry {
            Message = new SeStringBuilder()
                .AddUiForeground($"[{Name}] ", 62)
                .Append(messageTemplate)
                .Build()
        });
    }
}

internal static class TweakMessageExtensions {
    internal static void ModuleMessage<T>(this string messageTemplate, T tweak) where T : Tweak
        => tweak.ModuleMessage(messageTemplate);

    internal static void ModuleMessage<T>(this SeString messageTemplate, T tweak) where T : Tweak
        => tweak.ModuleMessage(messageTemplate);
}
