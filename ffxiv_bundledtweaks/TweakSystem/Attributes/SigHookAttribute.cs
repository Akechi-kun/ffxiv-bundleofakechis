namespace ComplexTweaks.TweakSystem;

[AttributeUsage(AttributeTargets.Method)]
public class SigHookAttribute(string Signature) : Attribute
{
    public string Signature { get; } = Signature;
}
