using Dalamud.Plugin.Ipc;

namespace ComplexTweaks.IPC;

public sealed class NavmeshIPC : BaseIPC, IPluginService {
    public int InitOrder => 10;

    public override Ipc Id => Ipc.Navmesh;
    public override string Name => "vnavmesh";
    public override string Repo => Veyn;

    private readonly ICallGateSubscriber<bool> _navIsReady;
    private readonly ICallGateSubscriber<float> _navBuildProgress;
    private readonly ICallGateSubscriber<object> _pathStop;
    private readonly ICallGateSubscriber<bool> _pathIsRunning;
    private readonly ICallGateSubscriber<Vector3, bool, bool> _pathfindAndMoveTo;
    private readonly ICallGateSubscriber<bool> _pathfindInProgress;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> _pointOnFloor;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> _nearestPointReachable;
    private readonly ICallGateSubscriber<Vector3?> _flagToPoint;

    public NavmeshIPC() {
        _navIsReady = Svc.Interface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        _navBuildProgress = Svc.Interface.GetIpcSubscriber<float>("vnavmesh.Nav.BuildProgress");
        _pathStop = Svc.Interface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        _pathIsRunning = Svc.Interface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        _pathfindAndMoveTo = Svc.Interface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
        _pathfindInProgress = Svc.Interface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
        _pointOnFloor = Svc.Interface.GetIpcSubscriber<Vector3, bool, float, Vector3?>("vnavmesh.Query.Mesh.PointOnFloor");
        _nearestPointReachable = Svc.Interface.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPointReachable");
        _flagToPoint = Svc.Interface.GetIpcSubscriber<Vector3?>("vnavmesh.Query.Mesh.FlagToPoint");
    }

    public bool IsReady => _navIsReady.HasFunction && _navIsReady.InvokeFunc();
    public float BuildProgress => _navBuildProgress.HasFunction ? _navBuildProgress.InvokeFunc() : -1f;
    public bool PathfindInProgress => _pathfindInProgress.HasFunction && _pathfindInProgress.InvokeFunc();

    public void Stop() {
        if (_pathStop.HasAction)
            _pathStop.InvokeAction();
    }

    public bool IsRunning() => _pathIsRunning.HasFunction && _pathIsRunning.InvokeFunc();

    public bool PathfindAndMoveTo(Vector3 dest, bool fly = false)
        => _pathfindAndMoveTo.HasFunction && _pathfindAndMoveTo.InvokeFunc(dest, fly);

    public Vector3? PointOnFloor(Vector3 p, bool allowUnlandable = false, float halfExtentXZ = 5)
        => _pointOnFloor.HasFunction ? _pointOnFloor.InvokeFunc(p, allowUnlandable, halfExtentXZ) : null;

    public Vector3? NearestPointReachable(Vector3 p, float halfExtentXZ = 5, float halfExtentY = 5)
        => _nearestPointReachable.HasFunction ? _nearestPointReachable.InvokeFunc(p, halfExtentXZ, halfExtentY) : null;

    public Vector3? FlagToPoint()
        => _flagToPoint.HasFunction ? _flagToPoint.InvokeFunc() : null;
}
