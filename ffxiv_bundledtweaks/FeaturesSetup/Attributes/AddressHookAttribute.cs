namespace ComplexTweaks.FeaturesSetup;

[AttributeUsage(AttributeTargets.Method)]
public class AddressHookAttribute<T>(string AddressName) : Attribute
{
    public string AddressName { get; } = AddressName;
}
