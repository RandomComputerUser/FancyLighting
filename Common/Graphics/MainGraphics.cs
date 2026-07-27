using System.Reflection;
using Terraria.Graphics.Capture;

namespace FancyLighting.Common.Graphics;

internal static class MainGraphics
{
    public static bool DoingCapture { get; private set; }
    public static RenderTarget2D ScreenTarget;
    public static RenderTarget2D ScreenTargetSwap;

    public static bool InCameraMode { get; private set; }
    public static bool CameraModeCaptureBackground { get; private set; }
    public static CaptureBiome CameraModeBiome { get; private set; }
    private static object _captureCamera;

    private static FieldInfo _field_filterFrameBuffer1;
    private static FieldInfo _field_filterFrameBuffer2;

    private static Stack<(
        int slot,
        Texture texture,
        SamplerState samplerState
    )> _savedTextures = new();

    internal static void AddHooks()
    {
        On_CaptureCamera.DrawTick += _CaptureCamera_DrawTick;
        On_Main.DrawCapture += _Main_DrawCapture;
    }

    internal static void Unload()
    {
        ScreenTarget = null;
        ScreenTargetSwap = null;

        CameraModeBiome = null;
        _captureCamera = null;

        _field_filterFrameBuffer1 = null;
        _field_filterFrameBuffer2 = null;

        _savedTextures = null;
    }

    internal static void ResetCaptureInfo()
    {
        DoingCapture = false;
        ScreenTarget = null;
        ScreenTargetSwap = null;
    }

    internal static void BeginCapture()
    {
        if (!InCameraMode)
        {
            ScreenTarget = Main.screenTarget;
            ScreenTargetSwap = Main.screenTargetSwap;
        }

        DoingCapture = true;
    }

    internal static void EndCapture_Pre() => DoingCapture = false;

    internal static void EndCapture_Post()
    {
        if (!CompatibilityConfig.Instance.DisableRenderingOptimizations)
        {
            if (InCameraMode && _captureCamera is not null)
            {
                _field_filterFrameBuffer1?.SetValue(_captureCamera, ScreenTarget);
                _field_filterFrameBuffer2?.SetValue(_captureCamera, ScreenTargetSwap);
            }
            else
            {
                Main.screenTarget = ScreenTarget;
                Main.screenTargetSwap = ScreenTargetSwap;
            }
        }

        ScreenTarget = null;
        ScreenTargetSwap = null;
    }

    private static void _CaptureCamera_DrawTick(
        On_CaptureCamera.orig_DrawTick orig,
        object self
    )
    {
        _field_filterFrameBuffer1 ??= self.GetType()
            .GetField(
                "_filterFrameBuffer1",
                BindingFlags.NonPublic | BindingFlags.Instance
            )
            .AssertNotNull();

        _field_filterFrameBuffer2 ??= self.GetType()
            .GetField(
                "_filterFrameBuffer2",
                BindingFlags.NonPublic | BindingFlags.Instance
            )
            .AssertNotNull();

        _captureCamera = self;
        var target = (RenderTarget2D)_field_filterFrameBuffer1.GetValue(self);
        TextureUtils.EnsureFormat(ref target, TextureUtils.ScreenFormat);
        _field_filterFrameBuffer1.SetValue(self, target);
        ScreenTarget = target;
        target = (RenderTarget2D)_field_filterFrameBuffer2.GetValue(self);
        TextureUtils.EnsureFormat(ref target, TextureUtils.ScreenFormat);
        _field_filterFrameBuffer2.SetValue(self, target);
        ScreenTargetSwap = target;

        InCameraMode = true;
        try
        {
            orig(self);
        }
        finally
        {
            InCameraMode = false;
            _captureCamera = null;
        }
    }

    private static void _Main_DrawCapture(
        On_Main.orig_DrawCapture orig,
        Main self,
        Rectangle area,
        CaptureSettings settings
    )
    {
        CameraModeCaptureBackground = settings.CaptureBackground;
        CameraModeBiome = settings.Biome;
        ModContent.GetInstance<SettingsSystem>().SettingsUpdate();
        orig(self, area, settings);
        CameraModeBiome = null;
    }

    public static void ResetSavedTextures() => _savedTextures.Clear();

    public static void SetTexture(int slot, Texture texture, SamplerState samplerState)
    {
        _savedTextures.Push(
            (
                slot,
                Main.graphics.GraphicsDevice.Textures[slot],
                Main.graphics.GraphicsDevice.SamplerStates[slot]
            )
        );

        Main.graphics.GraphicsDevice.Textures[slot] = texture;
        Main.graphics.GraphicsDevice.SamplerStates[slot] = samplerState;
    }

    public static void RestoreSavedTextures()
    {
        while (_savedTextures.TryPop(out var textureInfo))
        {
            Main.graphics.GraphicsDevice.Textures[textureInfo.slot] = textureInfo.texture;
            Main.graphics.GraphicsDevice.SamplerStates[textureInfo.slot] =
                textureInfo.samplerState;
        }
    }
}
