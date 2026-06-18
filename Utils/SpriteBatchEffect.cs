namespace FancyLighting.Utils;

internal sealed class SpriteBatchEffect
{
    private Effect Effect { get; init; }

    private EffectTechnique Technique { get; init; }

    internal SpriteBatchEffect(Effect effect, EffectTechnique technique)
    {
        Effect = effect;
        Technique = technique;
    }

    public void Unload() => Effect?.Dispose();

    internal Effect ApplyTechnique()
    {
        Effect.CurrentTechnique = Technique;
        return Effect;
    }

    internal Effect ApplyPass()
    {
        Effect.CurrentTechnique = Technique;
        Effect.CurrentTechnique.Passes[0].Apply();
        return Effect;
    }

    public SpriteBatchEffect SetParameter(string parameterName, float value)
    {
        Effect.Parameters[parameterName]?.SetValue(value);
        return this;
    }

    public SpriteBatchEffect SetParameter(string parameterName, Vector2 value)
    {
        Effect.Parameters[parameterName]?.SetValue(value);
        return this;
    }

    public SpriteBatchEffect SetParameter(string parameterName, Vector3 value)
    {
        Effect.Parameters[parameterName]?.SetValue(value);
        return this;
    }

    public SpriteBatchEffect SetParameter(string parameterName, Vector4 value)
    {
        Effect.Parameters[parameterName]?.SetValue(value);
        return this;
    }

    public SpriteBatchEffect SetParameter(string parameterName, Matrix value)
    {
        Effect.Parameters[parameterName]?.SetValue(value);
        return this;
    }
}
