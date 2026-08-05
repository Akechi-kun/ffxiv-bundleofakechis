using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace ComplexTweaks.Tweaks;

[Tweak]
public partial class AntiStatus : Tweak {
    public override string Name => "Anti Status";
    public override string Description => "No more detrimental statuses";

    [SigHook("E8 ?? ?? ?? ?? 32 C0 EB 10")]
    private unsafe nint Bewitch(GameObject* gameObj, float x, float y, float z, int a5, nint a6) {
        try {
            if (gameObj->IsCharacter()) {
                var chara = gameObj->BattleChara();
                if (chara->GetStatusManager()->HasStatus(3023) || chara->GetStatusManager()->HasStatus(3024))
                    return nint.Zero;
            }
            return BewitchHook.Original(gameObj, x, y, z, a5, a6);
        }
        catch (Exception ex) {
            Svc.Log.Error(ex.Message, ex);
            return BewitchHook.Original(gameObj, x, y, z, a5, a6);
        }
    }

    [SigHook("E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? FF C6")]
    private long Knockback(long gameobj, float rot, float length, long a4, char a5, int a6) => KnockbackHook.Original(gameobj, rot, 0f, a4, a5, a6);
}
