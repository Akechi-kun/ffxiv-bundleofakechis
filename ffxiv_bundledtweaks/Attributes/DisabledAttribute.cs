namespace ComplexTweaks.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class DisabledAttribute(string? reason = null) : Attribute {
    public string? Reason { get; } = reason;
}
