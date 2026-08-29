using System.Reflection;

namespace ComplexTweaks.Services;

public sealed class IPCRegistry : IPluginService {
    private readonly Dictionary<Ipc, BaseIPC> _byId = [];

    public IPCRegistry() {
        foreach (var ipc in Svc.GetServices<BaseIPC>())
            _byId[ipc.Id] = ipc;
    }

    public BaseIPC? Get(Ipc id) => _byId.TryGetValue(id, out var ipc) ? ipc : null;

    public BaseIPC[] GetMany(params Ipc[] ids) {
        if (ids.Length == 0)
            return [];
        return [.. ids.Select(Get).Where(ipc => ipc != null).Cast<BaseIPC>()];
    }

    public bool AreAllLoaded(params Ipc[] ids) {
        if (ids.Length == 0)
            return true;

        if (ids.Any(id => !_byId.ContainsKey(id)))
            return false;

        var ipcs = GetMany(ids);
        return ipcs.Length == ids.Length && ipcs.All(ipc => ipc.IsLoaded);
    }

    public BaseIPC[] GetMissing(ICustomAttributeProvider? provider)
        => provider == null ? [] : GetMissing([.. provider.GetCustomAttributes(typeof(RequiresAttribute), inherit: false).Cast<RequiresAttribute>().SelectMany(r => r.Id.Flags).Where(id => id != Ipc.None).Distinct().ToArray()]);

    public BaseIPC[] GetMissing(MethodInfo? method) => GetMissing((ICustomAttributeProvider?)method);

    public BaseIPC[] GetMissing(Enum? value) {
        if (value == null) return [];
        var field = value.GetType().GetField(value.ToString()!);
        return GetMissing(field);
    }

    public BaseIPC[] GetMissing(params Ipc[] ids) {
        if (ids.Length == 0)
            return [];

        var missing = new List<BaseIPC>();
        foreach (var id in ids) {
            if (!_byId.TryGetValue(id, out var ipc))
                continue;
            if (!ipc.IsLoaded)
                missing.Add(ipc);
        }

        return [.. missing];
    }
}
