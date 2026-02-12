using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System.Threading.Tasks;

namespace ComplexTweaks.Tasks;

public abstract class GoToAetherTask : TaskBase {
    protected async Task<IGameObject?> GoToObject(
        Vector3 destination,
        uint territory,
        Func<IGameObject, bool>? condition = null,
        Func<IGameObject, bool>? waitUntil = null
    ) {
        using var scope = BeginScope("GoToAether");

        await TeleportTo(territory, destination);
        await Mount();
        await MoveTo(destination, MovementConfig.InteractRange);

        var obj = Svc.Objects
            .Where(o => o != null && o.IsValid() && o.ObjectKind == ObjectKind.EventObj)
            .OrderBy(o => Vector3.Distance(o.Position, destination))
            .FirstOrDefault(o => Vector3.Distance(o.Position, destination) <= 3f && (condition?.Invoke(o) ?? true));

        ErrorIf(obj == null, $"Failed to find object nearby");

        if (obj != null)
            await InteractWith(obj, () => waitUntil?.Invoke(obj) ?? true);

        return obj;
    }
}

public sealed class GoToAetherCurrentQuest(Quest questRow) : GoToAetherTask {
    private readonly Quest _questRow = questRow;

    protected override async Task Execute() {
        if (_questRow.IssuerLocation.RowId == 0)
            return;

        var level = _questRow.IssuerLocation.Value;
        var npc = await GoToObject(
            destination: level.ToVector3(),
            territory: level.Territory.RowId,
            waitUntil: o => AtkUnitBase.IsAddonReady("Talk") || AtkUnitBase.IsAddonReady("SelectString")
        );
    }
}

public sealed class GoToAetherCurrent(Level levelRow) : GoToAetherTask {
    private readonly Level _levelRow = levelRow;

    protected override async Task Execute() {
        var obj = await GoToObject(
            destination: _levelRow.ToVector3(),
            territory: _levelRow.Territory.RowId,
            waitUntil: o => !o.IsValid()
        );
    }
}

