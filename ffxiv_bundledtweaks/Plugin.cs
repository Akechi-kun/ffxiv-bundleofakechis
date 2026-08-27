using clib;
using ComplexTweaks.Configuration;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using System.Threading;
using System.Threading.Tasks;

namespace ComplexTweaks;

public sealed class Plugin(IDalamudPluginInterface dalamud) : IAsyncDalamudPlugin {
    public static string Name => "CBT";
    private const string Command = "/cbt";
    public static Plugin P { get; private set; } = null!;
    public static Config C { get; private set; } = null!;
    public string VersionString => Svc.Interface.Manifest.AssemblyVersion.ToString(2);

    public bool IsLocalCs { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken) {
        P = this;
#if LOCAL_CS
        dalamud.InitCustomClientStructs();
        IsLocalCs = true;
#endif
        ECommons.ECommonsMain.Init(dalamud, this);
        CLibMain.Init(dalamud, this, CLibModule.Automation);

        C = ConfigService.Get().Config;

        ICommandManager.Get().AddHandler(Command, new(OnCommand) { HelpMessage = $"Opens the {Name} menu" });

        await TweakService.Get().InitializeTweaksAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync() {
        ICommandManager.Get().RemoveHandler(Command);
        ConfigService.Get().Save();
        await CLibMain.DisposeAsync();
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
                    if (TweakService.Get().Tweaks.FirstOrDefault(t => t.InternalName == @params[0]) is { } tweak && !C.EnabledTweaks.Contains(tweak.InternalName) && (!tweak.IsDebug || C.ShowDebug))
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
                    foreach (var t in TweakService.Get().Tweaks)
                        t.StopAutomation();
                    foreach (var t in TweakService.Get().Tweaks.OfType<ARTweak>())
                        t.AutoRetainer.FinishCharacterPostProcess();
                    break;
                case "leave":
                    EventFramework.LeaveCurrentContent(true);
                    break;
            }
        }
    }
}
