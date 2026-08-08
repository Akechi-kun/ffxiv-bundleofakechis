using AutoRetainerAPI;
using System.Reflection;

namespace ComplexTweaks.Services;

public static class Service {
    public static Provider Provider => Svc.Get<Provider>();
    public static AutoRetainerApi AutoRetainerApi => AutoRetainerApiService.Get().Api;
    public static AutoRetainerIPC AutoRetainerIPC => Svc.Get<AutoRetainerIPC>();
    public static BossModIPC BossMod => Svc.Get<BossModIPC>();
    public static LifestreamIPC Lifestream => Svc.Get<LifestreamIPC>();
    public static NavmeshIPC Navmesh => Svc.Get<NavmeshIPC>();
    public static QuestionableIPC Questionable => Svc.Get<QuestionableIPC>();
    public static TextAdvanceIpc TextAdvance => Svc.Get<TextAdvanceIpc>();
    public static IPCRegistry IPC => Svc.Get<IPCRegistry>();
    public static Automation Automation => AutomationService.Get().Automation;
}

public sealed class AutoRetainerApiService : IPluginService, IDisposable {
    public int InitOrder => 10;
    public AutoRetainerApi Api { get; } = new();
    public void Dispose() => Api.Dispose();
}

public sealed class AutomationService : IPluginService, IDisposable {
    public int InitOrder => 10;
    public Automation Automation { get; } = new();
    public void Dispose() => Automation.Dispose();
}

public sealed class IPCRegistry : IPluginService {
    public int InitOrder => 50;

    private readonly Dictionary<Ipc, BaseIPC> _byId = [];

    public IPCRegistry() {
        BaseIPC[] ipcs = [
            AutoRetainerIPC.Get(),
            BossModIPC.Get(),
            LifestreamIPC.Get(),
            NavmeshIPC.Get(),
            QuestionableIPC.Get(),
            TextAdvanceIpc.Get(),
        ];
        foreach (var ipc in ipcs)
            MapByEnum(ipc);
    }

    private void MapByEnum(BaseIPC ipc) {
        if (ipc.GetType().GetCustomAttribute<IpcAttribute>(inherit: false) is { } attr)
            _byId[attr.Id] = ipc;
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

    public BaseIPC[] GetMissing(MethodInfo? method)
        => method == null ? [] : GetMissing([.. method.GetCustomAttributes<RequiresAttribute>().SelectMany(r => r.Id.Flags).Where(id => id != Ipc.None).Distinct().ToArray()]);

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
