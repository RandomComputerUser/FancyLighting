namespace FancyLighting.ColorGradients;

public interface ITimedBasedColorGradient
{
    public Vector3 GetColor(double hour);
}
