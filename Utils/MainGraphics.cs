using System.Reflection;
using FancyLighting.Utils.Accessors;
using MonoMod.RuntimeDetour;

namespace FancyLighting.Utils;

internal static class MainGraphics
{
    private static Stack<(
        int slot,
        Texture texture,
        SamplerState samplerState
    )> _savedTextures = new();

    private static RenderTarget2D[] _renderTargetsOverride = new RenderTarget2D[4];

    private static RenderTargetBinding[] _renderTargetBindings1 = new RenderTargetBinding[
        1
    ];
    private static RenderTargetBinding[] _renderTargetBindings2 = new RenderTargetBinding[
        2
    ];
    private static RenderTargetBinding[] _renderTargetBindings3 = new RenderTargetBinding[
        3
    ];
    private static RenderTargetBinding[] _renderTargetBindings4 = new RenderTargetBinding[
        4
    ];

    private static Hook _hook_GraphicsDevice_SetRenderTargets;

    internal static bool DisableRenderTargetsOverride { get; set; } = false;

    internal static void Load()
    {
        var detourMethod = typeof(GraphicsDevice).GetMethod(
            nameof(GraphicsDevice.SetRenderTargets),
            BindingFlags.Public | BindingFlags.Instance
        );
        if (detourMethod is not null)
        {
            try
            {
                _hook_GraphicsDevice_SetRenderTargets = new(
                    detourMethod,
                    _GraphicsDevice_SetRenderTargets,
                    true
                );
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }
    }

    internal static void Unload()
    {
        _savedTextures = null;
        _renderTargetsOverride = null;
        _renderTargetBindings1 = null;
        _renderTargetBindings2 = null;
        _renderTargetBindings3 = null;
        _renderTargetBindings4 = null;

        _hook_GraphicsDevice_SetRenderTargets?.Dispose();

        _hook_GraphicsDevice_SetRenderTargets = null;
    }

    public static RenderTarget2D GetRenderTarget()
    {
        var renderTargets = Main.graphics.GraphicsDevice.GetRenderTargets();
        var renderTarget =
            renderTargets is null || renderTargets.Length < 1
                ? null
                : (RenderTarget2D)renderTargets[0].RenderTarget;
        return renderTarget;
    }

    public static SamplerState GetSamplerState() =>
        SpriteBatchAccessors.samplerState(Main.spriteBatch) ?? SamplerState.LinearClamp;

    public static Matrix GetTransformMatrix() =>
        SpriteBatchAccessors.transformMatrix(Main.spriteBatch);

    internal static void ResetSavedTextures() => _savedTextures.Clear();

    internal static void SetTexture(int slot, Texture texture, SamplerState samplerState)
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

    internal static void RestoreSavedTextures()
    {
        while (_savedTextures.TryPop(out var textureInfo))
        {
            Main.graphics.GraphicsDevice.Textures[textureInfo.slot] = textureInfo.texture;
            Main.graphics.GraphicsDevice.SamplerStates[textureInfo.slot] =
                textureInfo.samplerState;
        }
    }

    internal static void SetRenderTargetsOverride(RenderTarget2D target)
    {
        _renderTargetsOverride[0] = target;
        _renderTargetsOverride[1] = null;
        _renderTargetsOverride[2] = null;
        _renderTargetsOverride[3] = null;
    }

    internal static void SetRenderTargetsOverride(
        RenderTarget2D target0,
        RenderTarget2D target1
    )
    {
        _renderTargetsOverride[0] = target0;
        _renderTargetsOverride[1] = target1;
        _renderTargetsOverride[2] = null;
        _renderTargetsOverride[3] = null;
    }

    internal static void SetRenderTargetsOverride(
        RenderTarget2D target0,
        RenderTarget2D target1,
        RenderTarget2D target2
    )
    {
        _renderTargetsOverride[0] = target0;
        _renderTargetsOverride[1] = target1;
        _renderTargetsOverride[2] = target2;
        _renderTargetsOverride[3] = null;
    }

    internal static void SetRenderTargetsOverride(
        RenderTarget2D target0,
        RenderTarget2D target1,
        RenderTarget2D target2,
        RenderTarget2D target3
    )
    {
        _renderTargetsOverride[0] = target0;
        _renderTargetsOverride[1] = target1;
        _renderTargetsOverride[2] = target2;
        _renderTargetsOverride[3] = target3;
    }

    internal static void ResetRenderTargetsOverride()
    {
        for (var i = 0; i < _renderTargetsOverride.Length; ++i)
        {
            _renderTargetsOverride[i] = null;
        }
    }

    private delegate void orig_GraphicsDevice_SetRenderTargets(
        GraphicsDevice self,
        RenderTargetBinding[] renderTargets
    );

    private static void _GraphicsDevice_SetRenderTargets(
        orig_GraphicsDevice_SetRenderTargets orig,
        GraphicsDevice self,
        RenderTargetBinding[] renderTargets
    )
    {
        if (
            DisableRenderTargetsOverride
            || renderTargets is null
            || _renderTargetsOverride[0] is null
        )
        {
            orig(self, renderTargets);
            return;
        }

        if (_renderTargetsOverride[1] is null)
        {
            _renderTargetBindings1[0] = new(_renderTargetsOverride[0]);
            orig(self, _renderTargetBindings1);
        }
        else if (_renderTargetsOverride[2] is null)
        {
            _renderTargetBindings2[0] = new(_renderTargetsOverride[0]);
            _renderTargetBindings2[1] = new(_renderTargetsOverride[1]);
            orig(self, _renderTargetBindings2);
        }
        else if (_renderTargetsOverride[3] is null)
        {
            _renderTargetBindings3[0] = new(_renderTargetsOverride[0]);
            _renderTargetBindings3[1] = new(_renderTargetsOverride[1]);
            _renderTargetBindings3[2] = new(_renderTargetsOverride[2]);
            orig(self, _renderTargetBindings3);
        }
        else
        {
            _renderTargetBindings4[0] = new(_renderTargetsOverride[0]);
            _renderTargetBindings4[1] = new(_renderTargetsOverride[1]);
            _renderTargetBindings4[2] = new(_renderTargetsOverride[2]);
            _renderTargetBindings4[3] = new(_renderTargetsOverride[3]);
            orig(self, _renderTargetBindings4);
        }
    }
}
