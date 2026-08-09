using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Events;

namespace ComplexTweaks.Tweaks;

public class ClickToMoveConfiguration {
    [BoolConfig(Label = "In-World Click")] public bool WorldClickEnabled = true;
    [EnumConfig(Label = "Movement Type", DependsOn = nameof(WorldClickEnabled))] public MovementType WorldClickMovement = MovementType.Direct;

    [BoolConfig(Label = "AreaMap Click")] public bool MapClickEnabled = true;
    [EnumConfig(Label = "Movement Type", DependsOn = nameof(MapClickEnabled))] public MovementType MapClickMovement = MovementType.Direct;

    [EnumConfig(Label = "Modifier Key")] public ClickModifierKeys ClickModifier = ClickModifierKeys.Shift;
}

public unsafe partial class ClickToMove : Tweak<ClickToMoveConfiguration> {
    public override string Name => "Click to Move";
    public override string Description => "Like those other games. Supports clicking on the map.";

    private OverrideMovement movement = null!;

    public override void Enable() {
        movement = new();
        IAddonLifecycle.Get().RegisterListener(AddonEvent.PostReceiveEvent, "AreaMap", HandleMapClick);
    }

    public override void Disable() {
        movement.Dispose();
        IAddonLifecycle.Get().UnregisterListener(HandleMapClick);
    }

    private bool CanPathfind(MovementType type) => type is MovementType.Pathfind && IPCRegistry.Get().GetMissing(type).Length == 0;

    private void HandleMapClick(AddonEvent type, AddonArgs args) {
        if (!Config.MapClickEnabled || IObjectTable.Get().LocalPlayer is not { } player) return;
        if (args is AddonReceiveEventArgs { AtkEventType: AddonEventType.MouseDown } receiveArgs) {
            if (receiveArgs.AtkEventData.Cast<AtkEventData.AtkMouseData>()->ButtonId != 0) return; // left click only
            if (AgentMap.Instance()->CurrentMapId != AgentMap.Instance()->SelectedMapId) return;
            var success = Config.ClickModifier switch {
                ClickModifierKeys.None => true,
                ClickModifierKeys.Shift => receiveArgs.AtkEventData.Cast<AtkEventData>()->MouseData.Modifier.HasFlag(ModifierFlag.Shift),
                ClickModifierKeys.Ctrl => receiveArgs.AtkEventData.Cast<AtkEventData>()->MouseData.Modifier.HasFlag(ModifierFlag.Ctrl),
                ClickModifierKeys.Alt => receiveArgs.AtkEventData.Cast<AtkEventData>()->MouseData.Modifier.HasFlag(ModifierFlag.Alt),
                _ => false
            };
            if (!success) return;

            if (args.GetAddon<AddonAreaMap>()->GetMouseWorldCoords() is { } coords) {
                if (CanPathfind(Config.MapClickMovement))
                    NavmeshIPC.Get().PathfindAndMoveTo(coords.OnMesh(), Control.CanFly);
                else {
                    movement.Enabled = true;
                    movement.DesiredPosition = new(coords.X, player.Position.Y, coords.Y);
                }
            }
        }
    }

    [AddressHook<AtkInputManager>(nameof(AtkInputManager.MemberFunctionPointers.HandleInput))]
    internal unsafe void AtkInputManager_HandleInput(AtkInputManager* thisPtr, AtkUnitManager* unitManager, AtkCollisionManager* collisionManager) {
        AtkInputManager_HandleInputHook.Original(thisPtr, unitManager, collisionManager);
        if (IObjectTable.Get().LocalPlayer is not { } player) return;

        if (movement.Enabled && Vector3.Distance(movement.DesiredPosition, player.Position) <= 0.05f) movement.Enabled = false;

        if (!InputId.MOUSE_OK.IsReleased()) return;
        var modifierOk = Config.ClickModifier switch {
            ClickModifierKeys.None => true,
            ClickModifierKeys.Shift => UIInputData.Instance()->CurrentKeyModifier.HasFlag(KeyModifierFlag.Shift),
            ClickModifierKeys.Ctrl => UIInputData.Instance()->CurrentKeyModifier.HasFlag(KeyModifierFlag.Ctrl),
            ClickModifierKeys.Alt => UIInputData.Instance()->CurrentKeyModifier.HasFlag(KeyModifierFlag.Alt),
            _ => false
        };
        if (!modifierOk) return;

        if (!Config.WorldClickEnabled) return;
        if (Framework.Instance()->WindowInactive) return;
        if (player is not { Available: true, IsBusy: false }) return;
        if (!Utils.IsClickingInGameWorld()) return;

        IGameGui.Get().ScreenToWorld(ImGui.GetIO().MousePos, out var pos, 100000f);
        if (CanPathfind(Config.WorldClickMovement)) {
            if (NavmeshIPC.Get().IsRunning()) NavmeshIPC.Get().Stop();
            NavmeshIPC.Get().PathfindAndMoveTo(pos, false);
        }
        else {
            movement.Enabled = true;
            movement.DesiredPosition = pos;
        }
    }
}
