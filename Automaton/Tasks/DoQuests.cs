using System.Threading.Tasks;

namespace Automaton.Tasks;
public sealed class DoQuests(List<string> questIds) : CommonTasks
{
    protected override async Task Execute()
    {
        foreach (var quest in questIds)
        {
            Status = $"Doing quest #{quest}";
            if (Service.Questionable.StartSingleQuest(quest))
                await WaitWhile(() => !Game.IsQuestComplete(uint.Parse(quest)), $"QuestionableWaitForFinish{quest}", 120);
            else
                Error($"Failed to start quest #{quest}");
        }
        Status = "Going home";
        Service.Lifestream.ExecuteCommand("auto");
        await WaitUntilThenFalse(() => Service.Lifestream.IsBusy(), "LifestreamWaitForFinish");
    }
}
