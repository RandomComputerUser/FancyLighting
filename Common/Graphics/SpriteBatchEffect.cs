namespace FancyLighting.Common.Graphics;

internal class SpriteBatchEffect(
    Effect effect,
    string techniqueName,
    EffectFeatures features = EffectFeatures.None
) : FancyEffect(effect, techniqueName, features)
{
    public SpriteBatchEffect SetSpriteBatchTransform(Matrix transformMatrix)
    {
        // Code adapted from SpriteBatch code

        var viewport = Main.graphics.GraphicsDevice.Viewport;
        var tfWidth = (float)(2.0 / viewport.Width);
        var tfHeight = (float)(-2.0 / viewport.Height);

        var dstMatrix = transformMatrix;
        dstMatrix.M11 = (tfWidth * transformMatrix.M11) - transformMatrix.M14;
        dstMatrix.M21 = (tfWidth * transformMatrix.M21) - transformMatrix.M24;
        dstMatrix.M31 = (tfWidth * transformMatrix.M31) - transformMatrix.M34;
        dstMatrix.M41 = (tfWidth * transformMatrix.M41) - transformMatrix.M44;
        dstMatrix.M12 = (tfHeight * transformMatrix.M12) + transformMatrix.M14;
        dstMatrix.M22 = (tfHeight * transformMatrix.M22) + transformMatrix.M24;
        dstMatrix.M32 = (tfHeight * transformMatrix.M32) + transformMatrix.M34;
        dstMatrix.M42 = (tfHeight * transformMatrix.M42) + transformMatrix.M44;

        return SetParameter("MatrixTransform", dstMatrix);
    }

    public new SpriteBatchEffect SetParameter(string parameterName, float value)
    {
        base.SetParameter(parameterName, value);
        return this;
    }

    public new SpriteBatchEffect SetParameter(string parameterName, Vector2 value)
    {
        base.SetParameter(parameterName, value);
        return this;
    }

    public new SpriteBatchEffect SetParameter(string parameterName, Vector3 value)
    {
        base.SetParameter(parameterName, value);
        return this;
    }

    public new SpriteBatchEffect SetParameter(string parameterName, Vector4 value)
    {
        base.SetParameter(parameterName, value);
        return this;
    }

    public new SpriteBatchEffect SetParameter(string parameterName, Matrix value)
    {
        base.SetParameter(parameterName, value);
        return this;
    }
}
