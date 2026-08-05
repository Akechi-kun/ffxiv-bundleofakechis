namespace ComplexTweaks.IPC;

[Flags]
public enum Ipc {
    None = 0,
    AutoRetainer = 1 << 0,
    BossMod = 1 << 1,
    Lifestream = 1 << 4,
    Navmesh = 1 << 5,
    Questionable = 1 << 6,
    TextAdvance = 1 << 12,
}
