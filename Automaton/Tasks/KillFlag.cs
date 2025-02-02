using Dalamud.Game.ClientState.Objects.Types;
using System.Threading.Tasks;

namespace Automaton.Tasks;
public sealed class KillFlag : CommonTasks
{
    protected override async Task Execute()
    {
        await TeleportTo(PlayerEx.MapFlag.TerritoryId, Coords.FlagToWorld(PlayerEx.MapFlag));
        await MoveTo(PlayerEx.MapFlag, 5, true, true);
        using var stop = new OnDispose(() => Service.BossMod.ClearActive());
        await Kill();
    }

    private async Task Kill()
    {
        // TODO: maybe order by rank then get first in case a B is next to an A or something
        if (Svc.Objects.FirstOrDefault(o => o is IBattleNpc mob && mob.IsHunt(), null) is { } target)
        {
            Svc.Targets.Target = target;
            Service.BossMod.SetActive("VBM Default");
            Status = $"Waiting for {target.Name} to die";
            await TargetDead(target);
            Service.BossMod.ClearActive();
        }
    }

    private async Task TargetDead(DGameObject target)
    {
        while (target != null && !target.IsDead)
            await NextFrame(30);
    }
}
