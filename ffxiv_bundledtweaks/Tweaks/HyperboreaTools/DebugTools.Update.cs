using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace ComplexTweaks.Tweaks;

public partial class DebugTools : Tweak<DebugToolsConfiguration> {
    [FrameworkUpdate(nameof(Config.EnableTPClick))]
    private unsafe void OnTeleportClickUpdate(IFramework framework) {
        if (IObjectTable.Get().LocalPlayer is not { } player || ICondition.Get().IsUnavailable()) return;

        ShowMouseOverlay = false;
        if (!tpActive)
            return;

        if (!Framework.Instance()->WindowInactive && SeVirtualKey.CONTROL.IsDown() && Utils.IsClickingInGameWorld()) {
            ShowMouseOverlay = true;
            var pos = ImGui.GetMousePos();
            if (Svc.GameGui.ScreenToWorld(pos, out var res)) {
                if (MouseButtonFlags.LBUTTON.IsPressed()) {
                    if (!IsLButtonPressed)
                        player.SetPosition(res);
                    IsLButtonPressed = true;
                }
                else
                    IsLButtonPressed = false;
            }
        }
    }

    [FrameworkUpdate(nameof(Config.EnableNoClip))]
    private unsafe void OnNoClipUpdate(IFramework framework) {
        if (IObjectTable.Get().LocalPlayer is not { } player || ICondition.Get().IsUnavailable()) return;
        if (!ncActive || Framework.Instance()->WindowInactive)
            return;

        var cx = player.Position.X;
        var cy = player.Position.Z;
        var angle = MathF.PI - CameraManager.Instance()->GetActiveCamera()->DirH;
        if (_keys["JUMP"].IsHeldRaw())
            player.SetPosition((player.Position.X, player.Position.Y + Config.NoClipSpeed, player.Position.Z).ToVector3());
        if (SeVirtualKey.SHIFT.IsDown())
            player.SetPosition((player.Position.X, player.Position.Y - Config.NoClipSpeed, player.Position.Z).ToVector3());
        if (_keys["MOVE_FORE"].IsHeldRaw())
            player.SetPosition(player.Position.AddZ(Config.NoClipSpeed).RotatePoint(cx, cy, angle));
        if (_keys["MOVE_BACK"].IsHeldRaw())
            player.SetPosition(player.Position.AddZ(-Config.NoClipSpeed).RotatePoint(cx, cy, angle));
        if (_keys["MOVE_LEFT"].IsHeldRaw() || _keys["MOVE_STRIFE_L"].IsHeldRaw())
            player.SetPosition(player.Position.AddX(Config.NoClipSpeed).RotatePoint(cx, cy, angle));
        if (_keys["MOVE_RIGHT"].IsHeldRaw() || _keys["MOVE_STRIFE_R"].IsHeldRaw())
            player.SetPosition(player.Position.AddX(-Config.NoClipSpeed).RotatePoint(cx, cy, angle));
    }
}

