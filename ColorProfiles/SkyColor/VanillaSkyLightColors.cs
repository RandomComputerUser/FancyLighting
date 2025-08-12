namespace FancyLighting.ColorProfiles.SkyColor;

public class VanillaSkyLightColors : ISimpleColorProfile
{
    public static VanillaSkyLightColors Instance { get; } = new();

    private VanillaSkyLightColors() { }

    public Vector3 GetColor(double hour)
    {
        // This code is adapted from vanilla

        var dayTime = hour is >= 4.5 and < 19.5;
        var time =
            3600.0
            * (
                hour
                - (
                    dayTime ? 4.5
                    : hour < 12.0 ? -4.5
                    : 19.5
                )
            );

        var color = new Vector3(255f);
        float level;
        if (dayTime)
        {
            if (time < 13500.0)
            {
                level = (float)(time / 13500.0);
                color.X = (level * 230f) + 25f;
                color.Y = (level * 220f) + 35f;
                color.Z = (level * 220f) + 35f;
            }
            if (time > 45900.0)
            {
                level = (float)(1.0 - (((time / 54000.0) - 0.85) * 6.666666666666667));
                color.X = (level * 200f) + 35f;
                color.Y = (level * 85f) + 35f;
                color.Z = (level * 135f) + 35f;
            }
            else if (time > 37800.0)
            {
                level = (float)(1.0 - (((time / 54000.0) - 0.7) * 6.666666666666667));
                color.X = (level * 20f) + 235f;
                color.Y = (level * 135f) + 120f;
                color.Z = (level * 85f) + 170f;
            }
        }
        else
        {
            if (time < 16200.0)
            {
                level = (float)(1.0 - (time / 16200.0));
                color.X = (level * 30f) + 5f;
                color.Y = (level * 30f) + 5f;
                color.Z = (level * 30f) + 5f;
            }
            else
            {
                level = (float)(((time / 32400.0) - 0.5) * 2.0);
                color.X = (level * 20f) + 5f;
                color.Y = (level * 30f) + 5f;
                color.Z = (level * 30f) + 5f;
            }
        }

        return Vector3.Clamp(color / 255f, Vector3.Zero, Vector3.One);
    }
}
