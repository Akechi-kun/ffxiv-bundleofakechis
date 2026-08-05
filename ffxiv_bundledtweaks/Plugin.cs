using clib;
using ComplexTweaks.Configuration;
using ComplexTweaks.UI;
using Dalamud.Plugin;
using ECommons.Configuration;
using ECommons.SimpleGui;
using ECommons.Singletons;
using System.Collections.Specialized;
using System.Reflection;

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
        CLibMain.Init(pluginInterface, P, CLibModule.Automation);

        EzConfig.DefaultSerializationFactory = new YamlFactory();
        C = EzConfig.Init<Config>();

        IMigration[] migrations = [new V3(), new V4()];
        foreach (var migration in migrations) {
            if (C.Version < migration.Version) {
                Svc.Log.Info($"Migrating from config version {C.Version} to {migration.Version}");
                var c = C;
                migration.Migrate(ref c);
                C = c;
                C.Version = migration.Version;
            }
        }

        ICommandManager.Get().AddHandler(Command, new(OnCommand) { HelpMessage = $"Opens the {Name} menu" });
        EzConfigGui.Init(new HaselWindow(), nameOverride: $"{Name} v{P.VersionString}");
        EzConfigGui.WindowSystem.AddWindow(new DebugWindow());

        SingletonServiceManager.Initialize(typeof(Service));

        Svc.Framework.RunOnFrameworkThread(InitializeTweaks);
        C.EnabledTweaks.CollectionChanged += OnChange;
        Svc.Interface.ActivePluginsChanged += OnPluginsChanged;
    }

    public static void OnChange(object? sender, NotifyCollectionChangedEventArgs e) {
        foreach (var t in Tweaks) {
            if (C.EnabledTweaks.Contains(t.InternalName) && !t.Enabled)
                t.EnableInternal();
            else if (!C.EnabledTweaks.Contains(t.InternalName) && t.Enabled || t.Enabled && t.IsDebug && !C.ShowDebug)
                t.DisableInternal();
            EzConfig.Save();
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
            Svc.Log.Debug($"Disposing {tweak.InternalName}");
            tweak.DisposeInternal();
        }
        C.EnabledTweaks.CollectionChanged -= OnChange;
        Svc.Interface.ActivePluginsChanged -= OnPluginsChanged;
        CLibMain.Dispose();
    }

    private void OnCommand(string command, string args) {
        if (args.Length == 0)
            EzConfigGui.Window?.Toggle();
        else {
            var arguments = args.Split(' ');
            var subcommand = arguments[0];
            var @params = arguments.Skip(1).ToArray();
            switch (subcommand) {
                case string cmd when cmd.StartsWith('d') && !cmd.EqualsIgnoreCase("disable"):
                    EzConfigGui.GetWindow<DebugWindow>()!.Toggle();
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
                    Service.TaskManager.Abort();
                    foreach (var t in Tweaks.OfType<ARTweak>())
                        t.AutoRetainer.FinishCharacterPostProcess();
                    break;
            }
        }
    }

    private void InitializeTweaks() {
        foreach (var tweakType in GetType().Assembly.GetTypes().Where(type => type.GetCustomAttribute<TweakAttribute>() != null)) {
            Svc.Log.Verbose($"Initializing {tweakType.Name}");
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
