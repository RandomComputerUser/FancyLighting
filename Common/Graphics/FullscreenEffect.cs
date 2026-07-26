namespace FancyLighting.Common.Graphics;

internal class FullscreenEffect(
    Effect effect,
    string techniqueName,
    EffectFeatures features = EffectFeatures.None
) : FancyEffect(effect, techniqueName, features)
{
    public new FullscreenEffect SetParameter(string parameterName, float value)
    {
        base.SetParameter(parameterName, value);
        return this;
    }

    public new FullscreenEffect SetParameter(string parameterName, Vector2 value)
    {
        base.SetParameter(parameterName, value);
        return this;
    }

    public new FullscreenEffect SetParameter(string parameterName, Vector3 value)
    {
        base.SetParameter(parameterName, value);
        return this;
    }

    public new FullscreenEffect SetParameter(string parameterName, Vector4 value)
    {
        base.SetParameter(parameterName, value);
        return this;
    }

    public new FullscreenEffect SetParameter(string parameterName, Matrix value)
    {
        base.SetParameter(parameterName, value);
        return this;
    }
}
