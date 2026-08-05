using Dalamud.Game.ClientState.Objects.Enums;
using System.Threading.Tasks;

namespace ComplexTweaks.Tasks;

public sealed class GatherLeve : TaskBase {
    private DGameObject? LeveNode => IObjectTable.Get().FirstOrDefault(o => o is { IsTargetable: true, ObjectKind: ObjectKind.GatheringPoint, NameplateIconId: 71244 });
    protected override async Task Execute() {
        // travel to quest location
        // start leve
        // find the gathering point circles on the map
        // find nearest node and gather
        // repeat from 3 if no nearby nodes
        if (LeveNode is { } node) {
            await MoveTo(node.Position, MovementConfig.Everything);
            await InteractWith(node);
        }
    }
}
