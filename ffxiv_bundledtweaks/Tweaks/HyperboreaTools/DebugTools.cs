using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using ECommons.Interop;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;

namespace ComplexTweaks.Tweaks;

public class DebugToolsConfiguration {
    [BoolConfig] public bool AutoVoidIslandRest = false;
    [BoolConfig] public bool EnableTPClick = false;
    [BoolConfig] public bool EnableNoClip = false;

    [FloatConfig(DependsOn = nameof(EnableNoClip), DefaultValue = 0.05f)]
    public float NoClipSpeed = 0.05f;

    [BoolConfig] public bool EnableMoveSpeed = false;
    [BoolConfig] public bool EnableDirectActions = false;
    [BoolConfig] public bool EnableTPMarker = false;
    [BoolConfig] public bool EnableTPOffset = false;
    [BoolConfig] public bool EnableTPAbsolute = false;
}

[Tweak(true)]
public partial class DebugTools : Tweak<DebugToolsConfiguration> {
    public override string Name => "Debug Tools";
    public override string Description => "Debug tools for use in hyperborea/firewall";

    public override void Enable() {
        _keys = GetSheet<ConfigKey>().Where(x => x.RowId is >= 12 and <= 18).ToDictionary(x => x.Label.ToString(), x => x);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MJICraftSchedule", OnSetup);
        Svc.ClientState.EnterPvP += OnEnterPvP;
        Svc.Framework.Update += OnUpdate;
    }

    public override void Disable() {
        Svc.AddonLifecycle.UnregisterListener(OnSetup);
        Svc.ClientState.EnterPvP -= OnEnterPvP;
        Svc.Framework.Update -= OnUpdate;
    }

    private unsafe void OnUpdate(IFramework framework) {
        if (!Player.Available || IsOccupied()) return;

        ShowMouseOverlay = false;

        if (tpActive) {
            if (!Framework.Instance()->WindowInactive && IsKeyPressed([LimitedKeys.LeftControlKey, LimitedKeys.RightControlKey]) && Utils.IsClickingInGameWorld()) {
                ShowMouseOverlay = true;
                var pos = ImGui.GetMousePos();
                if (Svc.GameGui.ScreenToWorld(pos, out var res)) {
                    if (IsKeyPressed(LimitedKeys.LeftMouseButton)) {
                        if (!IsLButtonPressed)
                            Player.SetPosition(res);
                        IsLButtonPressed = true;
                    }
                    else
                        IsLButtonPressed = false;
                }
            }
        }

        if (ncActive && !Framework.Instance()->WindowInactive) {
            var cx = Player.Position.X;
            var cy = Player.Position.Z;
            var angle = MathF.PI - CameraManager.Instance()->GetActiveCamera()->DirH;
            if (_keys["JUMP"].IsHeldRaw())
                Player.SetPosition((Player.Position.X, Player.Position.Y + Config.NoClipSpeed, Player.Position.Z).ToVector3());
            if (Svc.KeyState.GetRawValue(VirtualKey.LSHIFT) != 0 || IsKeyPressed(LimitedKeys.LeftShiftKey))
                Player.SetPosition((Player.Position.X, Player.Position.Y - Config.NoClipSpeed, Player.Position.Z).ToVector3());
            if (_keys["MOVE_FORE"].IsHeldRaw())
                Player.SetPosition(Player.Position.AddZ(Config.NoClipSpeed).RotatePoint(cx, cy, angle));
            if (_keys["MOVE_BACK"].IsHeldRaw())
                Player.SetPosition(Player.Position.AddZ(-Config.NoClipSpeed).RotatePoint(cx, cy, angle));
            if (_keys["MOVE_LEFT"].IsHeldRaw() || _keys["MOVE_STRIFE_L"].IsHeldRaw())
                Player.SetPosition(Player.Position.AddX(Config.NoClipSpeed).RotatePoint(cx, cy, angle));
            if (_keys["MOVE_RIGHT"].IsHeldRaw() || _keys["MOVE_STRIFE_R"].IsHeldRaw())
                Player.SetPosition(Player.Position.AddX(-Config.NoClipSpeed).RotatePoint(cx, cy, angle));
        }
    }

    public override void OnConfigChange(string fieldName) {
        if (fieldName == nameof(Config.EnableTPClick) && !Config.EnableTPClick) {
            tpActive = false;
            ShowMouseOverlay = false;
        }

        if (fieldName == nameof(Config.EnableNoClip) && !Config.EnableNoClip)
            ncActive = false;
    }

    private unsafe void OnSetup(AddonEvent type, AddonArgs args) {
        if (!Config.AutoVoidIslandRest) return;
        if (AgentMJICraftSchedule.Instance()->Data->RestCycles.Hex() != 8321u) {
            Svc.Log.Debug($"Setting rest: {8321u:X}");
            AgentMJICraftSchedule.Instance()->Data->NewRestCycles = 8321u;
            var eventData = stackalloc int[] { 0, 0, 0 };
            var atkvalues = new Span<AtkValue>([new() { Type = AtkValueType.Int, Int = 0 }]);
            AgentMJICraftSchedule.Instance()->AgentInterface.ReceiveEvent((AtkValue*)eventData, atkvalues.GetPointer(0), (uint)atkvalues.Length, 5); // 5 = eventKind
        }
    }

    // prevent entering pvp with debug options enabled
    private void OnEnterPvP() {
        Player.Speed = 1.0f;
        tpActive = false;
        ncActive = false;
        ShowMouseOverlay = false;
    }

    public static bool ShowMouseOverlay;
    private bool IsLButtonPressed;
    private bool tpActive;
    private bool ncActive;
    private Dictionary<string, ConfigKey> _keys = null!;
}
