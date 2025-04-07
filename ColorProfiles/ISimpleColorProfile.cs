namespace FancyLighting.ColorProfiles;

public interface ISimpleColorProfile
{
    public Vector3 GetColor(double hour);
}
