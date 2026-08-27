using System.Threading;
using System.Threading.Tasks;

namespace ComplexTweaks.TweakSystem;

public interface ITweak : IDisposable {
    Type CachedType { get; }
    string InternalName { get; }
    IncompatibilityWarningAttribute[] IncompatibilityWarnings { get; }

    string Name { get; }
    string Description { get; }

    TweakStatus Status { get; }

    void OnEnable();
    void OnDisable();

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);

    void DrawConfig();
    void OnConfigChange(string fieldName);
}
