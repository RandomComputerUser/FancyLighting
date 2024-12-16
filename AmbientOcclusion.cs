using System;
using FancyLighting.Config;
using FancyLighting.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Capture;

namespace FancyLighting;

internal sealed class AmbientOcclusion
{
    private RenderTarget2D _blurTarget1;
    private RenderTarget2D _blurTarget2;

    private RenderTarget2D _drawTarget;

    private RenderTarget2D _cameraModeTarget1;
    private RenderTarget2D _cameraModeTarget2;

    private RenderTarget2D _tileEntityTarget;

    internal bool _drawingTileEntities;

    private Shader _extractInverseAlphaShader;
    private Shader _extractInverseMultipliedAlphaShader;
    private Shader _blurShader;
    private Shader _bilinearBlurShader;
    private Shader _finalBlurShader;
    private Shader _glowMaskShader;
    private Shader _enhancedGlowMaskShader;

    public AmbientOcclusion()
    {
        _extractInverseAlphaShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "ExtractInverseAlpha"
        );
        _extractInverseMultipliedAlphaShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "ExtractInverseMultipliedAlpha"
        );
        _blurShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "Blur"
        );
        _bilinearBlurShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "BilinearBlur"
        );
        _finalBlurShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "FinalBlur"
        );
        _glowMaskShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "GlowMask"
        );
        _enhancedGlowMaskShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "EnhancedGlowMask"
        );

        _drawingTileEntities = false;
    }

    public void Unload()
    {
        _blurTarget1?.Dispose();
        _blurTarget2?.Dispose();
        _cameraModeTarget1?.Dispose();
        _cameraModeTarget2?.Dispose();
        _tileEntityTarget?.Dispose();
        EffectLoader.UnloadEffect(ref _extractInverseAlphaShader);
        EffectLoader.UnloadEffect(ref _extractInverseMultipliedAlphaShader);
        EffectLoader.UnloadEffect(ref _blurShader);
        EffectLoader.UnloadEffect(ref _bilinearBlurShader);
        EffectLoader.UnloadEffect(ref _finalBlurShader);
        EffectLoader.UnloadEffect(ref _glowMaskShader);
        EffectLoader.UnloadEffect(ref _enhancedGlowMaskShader);
    }

    internal RenderTarget2D ApplyAmbientOcclusion(bool doDraw = true)
    {
        if (!LightingConfig.Instance.AmbientOcclusionEnabled())
        {
            return null;
        }

        TextureUtils.MakeSize(
            ref _blurTarget1,
            Main.instance.tileTarget.Width,
            Main.instance.tileTarget.Height,
            SurfaceFormat.Alpha8
        );
        TextureUtils.MakeSize(
            ref _blurTarget2,
            Main.instance.tileTarget.Width,
            Main.instance.tileTarget.Height,
            SurfaceFormat.Alpha8
        );
        if (doDraw)
        {
            TextureUtils.MakeSize(
                ref _drawTarget,
                Main.instance.tileTarget.Width,
                Main.instance.tileTarget.Height,
                TextureUtils.ScreenFormat
            );
        }

        var target = ApplyAmbientOcclusionInner(
            Main.instance.wallTarget,
            Main.instance.tileTarget,
            Main.instance.tile2Target,
            Main.sceneTilePos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
            Main.sceneTile2Pos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
            _blurTarget1,
            _blurTarget2,
            doDraw,
            _drawTarget
        );

        if (doDraw)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(Main.instance.wallTarget);
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(null);

        return doDraw ? null : target;
    }

    internal RenderTarget2D ApplyAmbientOcclusionCameraMode(
        RenderTarget2D screenTarget,
        RenderTarget2D wallTarget,
        CaptureBiome biome,
        bool doDraw = true,
        Texture2D glow = null,
        Texture2D lightedGlow = null
    )
    {
        TextureUtils.MakeSize(
            ref _blurTarget1,
            screenTarget.Width,
            screenTarget.Height,
            SurfaceFormat.Alpha8
        );
        TextureUtils.MakeSize(
            ref _blurTarget2,
            Main.instance.tileTarget.Width,
            Main.instance.tileTarget.Height,
            SurfaceFormat.Alpha8
        );
        TextureUtils.MakeSize(
            ref _cameraModeTarget1,
            screenTarget.Width,
            screenTarget.Height,
            TextureUtils.ScreenFormat
        );
        TextureUtils.MakeSize(
            ref _cameraModeTarget2,
            screenTarget.Width,
            screenTarget.Height,
            TextureUtils.ScreenFormat
        );

        Main.instance.TilesRenderer.SpecificHacksForCapture();

        var extraLayer =
            LightingConfig.Instance.DoNonSolidAmbientOcclusion
            || LightingConfig.Instance.DoTileEntityAmbientOcclusion;

        if (extraLayer)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTarget2);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
        }
        if (LightingConfig.Instance.DoNonSolidAmbientOcclusion)
        {
            // Set intoRenderTargets true to reset special tile counts
            Main.instance.TilesRenderer.PreDrawTiles(false, false, true);
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            if (biome is null)
            {
                Main.instance.TilesRenderer.Draw(false, false, false);
            }
            else
            {
                Main.instance.TilesRenderer.Draw(
                    false,
                    false,
                    false,
                    Main.bloodMoon ? 9 : biome.WaterStyle
                );
            }
            Main.tileBatch.End();
            Main.spriteBatch.End();
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTarget1);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);
        Main.instance.TilesRenderer.PreDrawTiles(true, false, false);
        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
        if (biome is null)
        {
            Main.instance.TilesRenderer.Draw(true, false, false);
        }
        else
        {
            Main.instance.TilesRenderer.Draw(
                true,
                false,
                false,
                Main.bloodMoon ? 9 : biome.WaterStyle
            );
        }
        Main.tileBatch.End();
        Main.spriteBatch.End();

        var target = ApplyAmbientOcclusionInner(
            wallTarget,
            _cameraModeTarget1,
            _cameraModeTarget2,
            Vector2.Zero,
            Vector2.Zero,
            _blurTarget1,
            _blurTarget2,
            doDraw,
            _cameraModeTarget2
        );

        if (doDraw)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTarget1);
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(screenTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            Main.graphics.GraphicsDevice.SetRenderTarget(screenTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(_cameraModeTarget1, Vector2.Zero, Color.White);
            if (glow is null)
            {
                Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
            }
            else
            {
                if (lightedGlow is null)
                {
                    _glowMaskShader
                        .SetParameter(
                            "GlowCoordMult",
                            new Vector2(
                                (float)target.Width / glow.Width,
                                (float)target.Height / glow.Height
                            )
                        )
                        .Apply();
                }
                else
                {
                    _enhancedGlowMaskShader
                        .SetParameter(
                            "GlowCoordMult",
                            new Vector2(
                                (float)target.Width / glow.Width,
                                (float)target.Height / glow.Height
                            )
                        )
                        .SetParameter(
                            "LightedGlowCoordMult",
                            new Vector2(
                                (float)target.Width / lightedGlow.Width,
                                (float)target.Height / lightedGlow.Height
                            )
                        )
                        .Apply();
                    Main.graphics.GraphicsDevice.Textures[5] = lightedGlow;
                    Main.graphics.GraphicsDevice.SamplerStates[5] =
                        SamplerState.PointClamp;
                }

                Main.graphics.GraphicsDevice.Textures[4] = glow;
                Main.graphics.GraphicsDevice.SamplerStates[4] = SamplerState.PointClamp;
                Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
            }
            Main.spriteBatch.End();
        }

        // Reset special tile counts
        Main.instance.TilesRenderer.PreDrawTiles(false, false, true);

        return doDraw ? null : target;
    }

    private RenderTarget2D ApplyAmbientOcclusionInner(
        RenderTarget2D wallTarget,
        RenderTarget2D tileTarget,
        RenderTarget2D tile2Target,
        Vector2 tileTargetPosition,
        Vector2 tile2TargetPosition,
        RenderTarget2D target1,
        RenderTarget2D target2,
        bool doDraw,
        RenderTarget2D drawTarget
    )
    {
        void ApplyBlurPass(
            ref bool useTarget2,
            int dx,
            int dy,
            Shader shader,
            float blurPower = 0f,
            float blurMult = 0f
        )
        {
            var destination = useTarget2 ? target2 : target1;
            var source = useTarget2 ? target1 : target2;

            Main.graphics.GraphicsDevice.SetRenderTarget(destination);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                shader == _blurShader
                    ? SamplerState.PointClamp
                    : SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );

            shader
                .SetParameter(
                    "BlurSize",
                    new Vector2((float)dx / source.Width, (float)dy / source.Height)
                )
                .SetParameter("BlurPower", blurPower)
                .SetParameter("BlurMult", blurMult)
                .Apply();

            Main.spriteBatch.Draw(source, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            useTarget2 = !useTarget2;
        }

        var drawNonSolidTiles = LightingConfig.Instance.DoNonSolidAmbientOcclusion;
        var drawTileEntities = LightingConfig.Instance.DoTileEntityAmbientOcclusion;

        if (!(drawNonSolidTiles || drawTileEntities))
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(target1);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            _extractInverseAlphaShader.Apply();
            Main.spriteBatch.Draw(tileTarget, tileTargetPosition, Color.White);
            Main.spriteBatch.End();
        }
        else
        {
            if (drawTileEntities)
            {
                TextureUtils.MakeSize(
                    ref _tileEntityTarget,
                    Main.instance.tileTarget.Width,
                    Main.instance.tileTarget.Height,
                    TextureUtils.ScreenFormat
                );

                Main.graphics.GraphicsDevice.SetRenderTarget(_tileEntityTarget);
                Main.graphics.GraphicsDevice.Clear(Color.Transparent);
                var currentZoom = Main.GameViewMatrix.Zoom;
                var currentScreenPosition = Main.screenPosition;
                Main.GameViewMatrix.Zoom = Vector2.One;
                Main.screenPosition -= new Vector2(Main.offScreenRange);

                _drawingTileEntities = true;
                try
                {
                    Main.instance.TilesRenderer.PostDrawTiles(false, false, false);
                    Main.instance.TilesRenderer.PostDrawTiles(true, false, false);
                }
                finally
                {
                    _drawingTileEntities = false;
                    Main.GameViewMatrix.Zoom = currentZoom;
                    Main.screenPosition = currentScreenPosition;
                }
            }

            Main.graphics.GraphicsDevice.SetRenderTarget(target1);
            Main.graphics.GraphicsDevice.Clear(Color.White);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendStates.Multiply,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );

            _extractInverseAlphaShader.Apply();
            Main.spriteBatch.Draw(tileTarget, tileTargetPosition, Color.White);

            _extractInverseMultipliedAlphaShader.Apply();

            if (drawNonSolidTiles && tile2Target is not null)
            {
                Main.spriteBatch.Draw(tile2Target, tile2TargetPosition, Color.White);
            }

            if (drawTileEntities)
            {
                Main.spriteBatch.Draw(
                    _tileEntityTarget,
                    Vector2.Zero,
                    null,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    1f,
                    Main.GameViewMatrix.Effects,
                    1f
                );
            }

            Main.spriteBatch.End();
        }

        var power = PreferencesConfig.Instance.AmbientOcclusionPower();
        if (LightingConfig.Instance.HiDefFeaturesEnabled())
        {
            power *= PreferencesConfig.Instance.GammaExponent();
        }
        var mult = PreferencesConfig.Instance.AmbientOcclusionMult();
        if (!LightingConfig.Instance.HiDefFeaturesEnabled())
        {
            mult = MathF.Pow(mult, 1.35f); // Crude approximation to try to make it look similar
        }

        var radius = PreferencesConfig.Instance.AmbientOcclusionRadius;
        var firstShaderBlurStep = radius switch
        {
            1 => 2,
            2 => 3,
            3 => 4,
            4 => 5,
            5 => 6,
            _ => 2,
        };

        var useTarget2 = true;
        ApplyBlurPass(ref useTarget2, firstShaderBlurStep, 0, _blurShader);
        ApplyBlurPass(ref useTarget2, 0, firstShaderBlurStep, _blurShader);
        ApplyBlurPass(ref useTarget2, 1, 0, _bilinearBlurShader);
        ApplyBlurPass(ref useTarget2, 0, 1, _finalBlurShader, power, mult);

        var target = useTarget2 ? target1 : target2;

        if (!doDraw)
        {
            return target;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(drawTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(wallTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendStates.MultiplyColorByAlpha,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

        return drawTarget;
    }
}
