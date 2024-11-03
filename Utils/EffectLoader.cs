using System;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace FancyLighting.Utils;

internal static class EffectLoader
{
    public static Shader LoadEffect(string filePath, string passName, bool hiDef = false)
    {
        var effect = ModContent
            .Request<Effect>(filePath, AssetRequestMode.ImmediateLoad)
            .Value;

        string hiDefPassName;
        if (hiDef)
        {
            hiDefPassName = passName + "HiDef";
        }
        else
        {
            hiDefPassName = null;
        }

        return new Shader(effect, passName, hiDefPassName);
    }

    public static void UnloadEffect(ref Shader shader)
    {
        try
        {
            shader.Unload();
        }
        catch (Exception) // Shouldn't normally happen
        { }
        finally
        {
            shader = null;
        }
    }
}
