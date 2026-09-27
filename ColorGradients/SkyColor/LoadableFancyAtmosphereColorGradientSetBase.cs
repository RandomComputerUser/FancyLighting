namespace FancyLighting.ColorGradients.SkyColor;

public abstract class LoadableFancyAtmosphereColorGradientSetBase
    : FancyAtmosphereColorGradientSetBase,
        ILoadable
{
    public virtual void Load(Mod mod) { }

    public virtual void Unload() { }
}
