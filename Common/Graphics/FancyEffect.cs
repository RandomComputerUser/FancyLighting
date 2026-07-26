namespace FancyLighting.Common.Graphics;

internal class FancyEffect
{
    public Effect Effect { get; private init; }

    public EffectTechnique Technique
    {
        get
        {
            var technique = SettingsSystem._lightOnly
                ? SettingsSystem._hiDef
                    ? _lightOnlyHiDefTechnique
                    : _lightOnlyTechnique
                : SettingsSystem._hiDef
                    ? _hiDefTechnique
                    : _baseTechnique;
            return technique ?? _baseTechnique;
        }
    }

    private readonly EffectTechnique _baseTechnique;
    private readonly EffectTechnique _hiDefTechnique;
    private readonly EffectTechnique _lightOnlyTechnique;
    private readonly EffectTechnique _lightOnlyHiDefTechnique;

    public FancyEffect(
        Effect effect,
        string techniqueName,
        EffectFeatures features = EffectFeatures.None
    )
    {
        Effect = effect;

        _baseTechnique = effect.Techniques[techniqueName];
        if ((features & EffectFeatures.HiDef) != 0)
        {
            _hiDefTechnique = effect.Techniques[techniqueName + "HiDef"];
        }
        if ((features & EffectFeatures.LightOnly) != 0)
        {
            _lightOnlyTechnique = effect.Techniques[techniqueName + "LightOnly"];
        }
        if ((features & EffectFeatures.LightOnlyHiDef) != 0)
        {
            _lightOnlyHiDefTechnique = effect.Techniques[
                techniqueName + "LightOnlyHiDef"
            ];
        }
    }

    public EffectTechnique ApplyTechnique()
    {
        Effect.CurrentTechnique = Technique;
        return Technique;
    }

    public EffectPass ApplyPass()
    {
        Effect.CurrentTechnique = Technique;
        var pass = Effect.CurrentTechnique.Passes[0];
        pass.Apply();
        return pass;
    }

    public FancyEffect SetParameter(string parameterName, float value)
    {
        Effect.Parameters[parameterName]?.SetValue(value);
        return this;
    }

    public FancyEffect SetParameter(string parameterName, Vector2 value)
    {
        Effect.Parameters[parameterName]?.SetValue(value);
        return this;
    }

    public FancyEffect SetParameter(string parameterName, Vector3 value)
    {
        Effect.Parameters[parameterName]?.SetValue(value);
        return this;
    }

    public FancyEffect SetParameter(string parameterName, Vector4 value)
    {
        Effect.Parameters[parameterName]?.SetValue(value);
        return this;
    }

    public FancyEffect SetParameter(string parameterName, Matrix value)
    {
        Effect.Parameters[parameterName]?.SetValue(value);
        return this;
    }
}
