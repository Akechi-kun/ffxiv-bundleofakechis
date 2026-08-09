using clib;
using ComplexTweaks.Configuration;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using System.Collections.Specialized;

namespace ComplexTweaks;

public class Plugin : IDalamudPlugin {
    public static string Name => "CBT";
    private const string Command = "/cbt";
    public static Plugin P { get; private set; } = null!;
    public static Config C { get; private set; } = null!;
    public string VersionString => Svc.Interface.Manifest.AssemblyVersion.ToString(2);

    public static readonly HashSet<Tweak> Tweaks = [];
    public readonly bool IsLocalCs;

    public Plugin(IDalamudPluginInterface pluginInterface, IDataManager data, ISigScanner sigs) {
        P = this;
#if LOCAL_CS
        pluginInterface.InitCustomClientStructs();
        IsLocalCs = true;
#endif
        ECommons.ECommonsMain.Init(pluginInterface, this);
        CLibMain.Init(pluginInterface, P, CLibModule.Automation);

        C = ConfigService.Get().Config;

        ICommandManager.Get().AddHandler(Command, new(OnCommand) { HelpMessage = $"Opens the {Name} menu" });

        IFramework.Get().RunOnFrameworkThread(InitializeTweaks);
        C.EnabledTweaks.CollectionChanged += OnChange;
        Svc.Interface.ActivePluginsChanged += OnPluginsChanged;
    }

    public static void OnChange(object? sender, NotifyCollectionChangedEventArgs e) {
        foreach (var t in Tweaks) {
            if (C.EnabledTweaks.Contains(t.InternalName) && !t.Enabled)
                t.EnableInternal();
            else if (!C.EnabledTweaks.Contains(t.InternalName) && t.Enabled || t.Enabled && t.IsDebug && !C.ShowDebug)
                t.DisableInternal();
            ConfigService.Get().Save();
        }
    }

    private static void OnPluginsChanged(IActivePluginsChangedEventArgs args) {
        foreach (var tweak in Tweaks) {
            if (C.EnabledTweaks.Contains(tweak.InternalName) && !tweak.Enabled && !tweak.Outdated && !tweak.Disabled)
                if (tweak.CanBeEnabled())
                    tweak.EnableInternal();

            if (tweak.Enabled && !tweak.CanBeEnabled())
                tweak.DisableInternal();

            if (tweak.Enabled && tweak.CanBeEnabled())
                tweak.RefreshCommands();
        }
    }

    public void Dispose() {
        ICommandManager.Get().RemoveHandler(Command);
        foreach (var tweak in Tweaks) {
            IPluginLog.Get().Debug($"Disposing {tweak.InternalName}");
            tweak.DisposeInternal();
        }
        C.EnabledTweaks.CollectionChanged -= OnChange;
        Svc.Interface.ActivePluginsChanged -= OnPluginsChanged;
        ConfigService.Get().Save();
        CLibMain.Dispose();
    }

    private void OnCommand(string command, string args) {
        if (args.Length == 0)
            WindowsService.Get().ToggleMain();
        else {
            var arguments = args.Split(' ');
            var subcommand = arguments[0];
            var @params = arguments.Skip(1).ToArray();
            switch (subcommand) {
                case string cmd when cmd.StartsWith('d') && !cmd.EqualsIgnoreCase("disable"):
                    WindowsService.Get().ToggleDebug();
                    break;
                case "enable":
                    if (Tweaks.FirstOrDefault(t => t.InternalName == @params[0]) is { } tweak && !C.EnabledTweaks.Contains(tweak.InternalName) && (!tweak.IsDebug || C.ShowDebug))
                        C.EnabledTweaks.Add(tweak.InternalName);
                    break;
                case "disable":
                    C.EnabledTweaks.Remove(@params[0]);
                    break;
                case "toggle":
                    if (C.EnabledTweaks.Contains(@params[0]))
                        C.EnabledTweaks.Remove(@params[0]);
                    else
                        C.EnabledTweaks.Add(@params[0]);
                    break;
                case "stop":
                    Svc.Automation.Stop();
                    Service.Automation.Stop();
                    foreach (var t in Tweaks)
                        t.StopAutomation();
                    foreach (var t in Tweaks.OfType<ARTweak>())
                        t.AutoRetainer.FinishCharacterPostProcess();
                    break;
                case "leave":
                    EventFramework.LeaveCurrentContent(true);
                    break;
            }
        }
    }

    private void InitializeTweaks() {
        foreach (var tweakType in GetType().Assembly.GetTypes()
                     .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(Tweak).IsAssignableFrom(type))) {
            IPluginLog.Get().Verbose($"Initializing {tweakType.Name}");
            try {
                Tweaks.Add((Tweak)Activator.CreateInstance(tweakType)!);
            }
            catch (Exception ex) {
                ex.Log($"Failed to initialize {tweakType.Name}");
            }
        }

        foreach (var tweak in Tweaks) {
            if (!C.EnabledTweaks.Contains(tweak.InternalName))
                continue;

            if (C.EnabledTweaks.Contains(tweak.InternalName) && tweak.IsDebug && !C.ShowDebug)
                C.EnabledTweaks.Remove(tweak.InternalName);

            tweak.EnableInternal();
        }
    }
}
