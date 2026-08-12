namespace FancyLighting.Common.Graphics;

internal static class CustomSamplerStates
{
    public static SamplerState LinearWrapUClampV { get; private set; } =
        CreateSamplerState(
            "LinearWrapUClampV",
            TextureFilter.Linear,
            addressU: TextureAddressMode.Wrap,
            addressV: TextureAddressMode.Clamp,
            addressW: TextureAddressMode.Wrap // don't care, use default value
        );

    internal static void Unload()
    {
        LinearWrapUClampV = null;
    }

    private static SamplerState CreateSamplerState(
        string name,
        TextureFilter filter,
        TextureAddressMode addressU,
        TextureAddressMode addressV,
        TextureAddressMode addressW
    )
    {
        var samplerState = new SamplerState();
        samplerState.Name = name;
        samplerState.Filter = filter;
        samplerState.AddressU = addressU;
        samplerState.AddressV = addressV;
        samplerState.AddressW = addressW;
        return samplerState;
    }
}
