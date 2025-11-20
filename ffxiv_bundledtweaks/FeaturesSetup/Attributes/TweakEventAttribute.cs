namespace ComplexTweaks.FeaturesSetup.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TweakEventAttribute(params TweakEvent[] events) : Attribute
{
    public TweakEvent[] Events { get; } = events;
}

