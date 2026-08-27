using Dalamud.Plugin;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

namespace ComplexTweaks.Services;

public sealed class TweakService : IPluginService, IAsyncDisposable {
    public readonly HashSet<Tweak> Tweaks = [];

    public TweakService() {
        C.EnabledTweaks.CollectionChanged += OnChange;
        Svc.Interface.ActivePluginsChanged += OnPluginsChanged;
    }

    public async Task InitializeTweaksAsync(CancellationToken cancellationToken) {
        foreach (var tweakType in GetType().Assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && typeof(Tweak).IsAssignableFrom(type))) {
            IPluginLog.Get().Verbose($"Initializing {tweakType.Name}");
            try {
                Tweaks.Add((Tweak)Activator.CreateInstance(tweakType)!);
            }
            catch (Exception ex) {
                ex.Log($"Failed to initialize {tweakType.Name}");
            }
        }

        await Task.WhenAll(Tweaks.Select(t => t.StartAsync(cancellationToken)));
    }

    private void OnChange(object? sender, NotifyCollectionChangedEventArgs e) {
        foreach (var t in Tweaks) {
            if (t.ShouldEnable())
                _ = t.StartAsync(CancellationToken.None);
            else if (t.ShouldDisable())
                _ = t.StopAsync(CancellationToken.None);
        }
        ConfigService.Get().Save();
    }

    private void OnPluginsChanged(IActivePluginsChangedEventArgs args) {
        foreach (var tweak in Tweaks) {
            if (C.EnabledTweaks.Contains(tweak.InternalName) && tweak.CanBeEnabled())
                _ = tweak.StartAsync(CancellationToken.None);

            if (tweak.Status == TweakStatus.Enabled && !tweak.HasRuntimeRequirements())
                _ = tweak.StopAsync(CancellationToken.None);

            if (tweak.Status == TweakStatus.Enabled)
                tweak.RefreshCommands();
        }
    }

    public ValueTask DisposeAsync() {
        C.EnabledTweaks.CollectionChanged -= OnChange;
        Svc.Interface.ActivePluginsChanged -= OnPluginsChanged;

        foreach (var t in Tweaks) {
            IPluginLog.Get().Debug($"Disposing {t.InternalName}");
            t.Dispose();
        }
        return ValueTask.CompletedTask;
    }
}
