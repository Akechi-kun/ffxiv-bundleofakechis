using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.STD;

namespace ComplexTweaks.Utilities.Extensions;
public static class ContentsFinderExtensions
{
    public static void ResetFlags(this ContentsFinder cf)
    {
        cf.IsExplorerMode = false;
        cf.IsLevelSync = false;
        cf.IsLimitedLevelingRoulette = false;
        cf.IsMinimalIL = false;
        cf.IsSilenceEcho = false;
        cf.IsUnrestrictedParty = false;
        cf.LootRules = ContentsFinder.LootRule.Normal;
    }

    public static unsafe uint* ToPtr(this StdVector<ContentsId> contentsIds)
    {
        var ids = contentsIds.Select(x => x.Id).ToList();
        var array = stackalloc uint[ids.Count];
        for (var i = 0; i < ids.Count; i++)
            array[i] = ids[i];
        return array;
    }
}
