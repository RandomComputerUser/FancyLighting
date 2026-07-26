namespace FancyLighting.Common.Graphics;

[Flags]
public enum EffectFeatures
{
    None = 0,
    HiDef = 1,
    LightOnly = 2,
    LightOnlyHiDef = 4,
    All = HiDef | LightOnly | LightOnlyHiDef,
}
