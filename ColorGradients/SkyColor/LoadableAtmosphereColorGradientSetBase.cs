namespace FancyLighting.ColorGradients.SkyColor;

public abstract class LoadableAtmosphereColorGradientSetBase
    : AtmosphereColorGradientSetBase,
        ILoadable
{
    public virtual void Load(Mod mod) { }

    public virtual void Unload() { }
}
