namespace FancyLighting.Utils;

internal class Shader
{
    protected Effect Effect { get; init; }

    private EffectPass _shader;
    private EffectPass _hiDefShader;

    protected EffectPass EffectPass =>
        _hiDefShader is not null && LightingConfig.Instance.HiDefFeaturesEnabled()
            ? _hiDefShader
            : _shader;

    public Shader(Effect effect, string passName, string hiDefPassName = "")
    {
        Effect = effect;

        _shader = effect.CurrentTechnique.Passes[passName];
        _hiDefShader = string.IsNullOrEmpty(hiDefPassName)
            ? null
            : effect.CurrentTechnique.Passes[hiDefPassName];
    }

    public void Unload() => Effect?.Dispose();

    public Shader SetParameter(string parameterName, float value)
    {
        Effect.Parameters[parameterName].SetValue(value);
        return this;
    }

    public Shader SetParameter(string parameterName, Vector2 value)
    {
        Effect.Parameters[parameterName].SetValue(value);
        return this;
    }

    public Shader SetParameter(string parameterName, Vector3 value)
    {
        Effect.Parameters[parameterName].SetValue(value);
        return this;
    }

    public Shader SetParameter(string parameterName, Vector4 value)
    {
        Effect.Parameters[parameterName].SetValue(value);
        return this;
    }

    public void Apply() => EffectPass?.Apply();
}
