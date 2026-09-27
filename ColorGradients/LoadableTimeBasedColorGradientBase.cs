namespace FancyLighting.ColorGradients;

public abstract class LoadableTimeBasedColorGradientBase
    : ILoadable,
        ITimedBasedColorGradient
{
    public virtual void Load(Mod mod) { }

    public virtual void Unload() { }

    public abstract Vector3 GetColor(double hour);
}
