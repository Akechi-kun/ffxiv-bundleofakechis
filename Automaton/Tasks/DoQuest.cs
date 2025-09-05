using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Threading.Tasks;

namespace Automaton.Tasks;
public sealed class DoQuest(Quest row) : CommonTasks
{
    protected override async Task Execute()
    {
        await MoveToLevelRow(row.IssuerLocation);
        //foreach (var objective in row.QuestParams.Where(p => !p.ScriptInstruction.IsEmpty))
        //    await DoObjective(objective);
    }

    private async Task MoveToLevelRow(RowRef<Level> row)
    {
        using var scope = BeginScope("MoveToLevelLocation");
        var destination = new Vector3(row.Value.X, row.Value.Y, row.Value.Z);
        if (Player.Territory != row.Value.Territory.RowId)
            await TeleportTo(row.Value.Territory.RowId, destination);

        await MoveTo(destination, MovementConfig.InteractRange);
    }

    private async Task DoObjective(Quest.QuestParamsStruct objective)
    {
        if (Enum.TryParse(objective.ScriptInstruction.ToString(), out ScriptInstruction instruction))
        {
            switch (instruction)
            {
                case ScriptInstruction.None:
                    break;
            }
        }
    }

    private enum ScriptInstruction
    {
        None = 0,
    }
}
