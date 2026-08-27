namespace ComplexTweaks.TweakSystem;

public abstract class ARTweak<T> : Tweak<T> {
    public ARTweak() : base() => AutoRetainer = new(Name);

    public AutoRetainerApi AutoRetainer { get; set; }

    public abstract void OnCharacterPostProcessStep();
    public abstract void OnCharacterReadyToPostProcess();

    public override void OnEnable() {
        AutoRetainer.OnCharacterPostprocessStep += OnCharacterPostProcessStep;
        AutoRetainer.OnCharacterReadyToPostProcess += OnCharacterReadyToPostProcess;
        base.OnEnable();
    }

    public override void OnDisable() {
        AutoRetainer.OnCharacterPostprocessStep -= OnCharacterPostProcessStep;
        AutoRetainer.OnCharacterReadyToPostProcess -= OnCharacterReadyToPostProcess;
        base.OnDisable();
    }

    public override void Dispose() {
        AutoRetainer.Dispose();
        base.Dispose();
    }
}
