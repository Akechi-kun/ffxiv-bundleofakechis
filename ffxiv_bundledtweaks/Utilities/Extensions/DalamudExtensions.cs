using Dalamud.Game.NativeWrapper;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ComplexTweaks.Utilities.Extensions;
public static unsafe class DalamudExtensions
{
    public static AtkUnitBase* ToPtr(this AddonArgs args) => (AtkUnitBase*)args.Addon.Address;
    public static AtkUnitBase* ToPtr(this AtkUnitBasePtr wrapper) => (AtkUnitBase*)wrapper.Address;

    public static bool AllTargetable(this IPartyList party) => party.All(p => p.GameObject?.IsTargetable ?? false);
}
