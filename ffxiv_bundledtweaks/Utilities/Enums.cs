namespace ComplexTweaks.Utilities;

public static class Enums {
    public enum MovementType {
        Direct,
        [Requires(Ipc.Navmesh)]
        Pathfind
    }

    public enum ClickModifierKeys {
        None,
        Shift,
        Ctrl,
        Alt,
    }

    public enum LinkHandlerId : uint {
        RelayLinkPayload,
    }
}
