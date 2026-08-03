using FancyLighting.VFX;
using Terraria.Graphics.Capture;

namespace FancyLighting.Core;

public sealed class AmbientOcclusion
{
    private RenderTarget2D _ambientOcclusionTarget;

    internal bool _drawingTileEntities;

    private readonly FullscreenEffect _tilesEffect;
    private readonly FullscreenEffect _tilesAndTiles2Effect;
    private readonly SpriteBatchEffect _tileEntityEffect;
    private readonly FullscreenEffect _toneCurveEffect;
    private readonly FullscreenEffect _toneCurveDefaultEffect;

    private readonly BlurRenderer _blurRenderer = new(true, false);

    internal AmbientOcclusion()
    {
        var effect = EffectLoader.Load("AmbientOcclusion");
        _tilesEffect = new(effect, "Tiles");
        _tilesAndTiles2Effect = new(effect, "TilesAndTiles2");
        _tileEntityEffect = new(effect, "TileEntity");
        _toneCurveEffect = new(effect, "ToneCurve", EffectFeatures.HiDef);
        _toneCurveDefaultEffect = new(effect, "ToneCurveDefault", EffectFeatures.HiDef);
    }

    internal void Unload()
    {
        _ambientOcclusionTarget?.Dispose();

        _blurRenderer?.Dispose();
    }

    internal RenderTarget2D DrawAmbientOcclusion(
        RenderTarget2D wallTarget,
        RenderTarget2D tileTarget,
        Vector2 tilePosition,
        RenderTarget2D tile2Target,
        Vector2 tile2Position,
        bool tileEntities,
        bool doScaling
    )
    {
        TextureUtils.MakeSize(
            ref _ambientOcclusionTarget,
            wallTarget.Width,
            wallTarget.Height,
            SurfaceFormat.Color
        );

        var effect = tile2Target is null ? _tilesEffect : _tilesAndTiles2Effect;

        var wallTexturePosition = doScaling
            ? TexturePosition.GetScreenPosition(wallTarget)
            : TexturePosition.GetTileTargetPosition(wallTarget);
        wallTexturePosition.TextureToWorldTransform(out var wallToWorldTransform);

        {
            var tileTexturePosition = doScaling
                ? wallTexturePosition
                : TexturePosition.GetTileTargetPosition(tileTarget, tilePosition);
            tileTexturePosition.WorldToTextureTransform(out var tileMatrixTransform);
            Matrix.Multiply(
                ref wallToWorldTransform,
                ref tileMatrixTransform,
                out tileMatrixTransform
            );
            effect.SetParameter("MatrixTransform", tileMatrixTransform);
        }

        MainGraphics.ResetSavedTextures();

        if (tile2Target is not null)
        {
            var tile2TexturePosition = doScaling
                ? wallTexturePosition
                : TexturePosition.GetTileTargetPosition(tile2Target, tile2Position);
            tile2TexturePosition.WorldToTextureTransform(out var tile2MatrixTransform);
            Matrix.Multiply(
                ref wallToWorldTransform,
                ref tile2MatrixTransform,
                out tile2MatrixTransform
            );
            effect.SetParameter("MatrixTransform2", tile2MatrixTransform);

            MainGraphics.SetTexture(4, tile2Target, SamplerState.PointClamp);
        }

        Blitter.Blit(tileTarget, _ambientOcclusionTarget, effect);
        MainGraphics.RestoreSavedTextures();

        if (tileEntities)
        {
            var prevPreventTileParticles = FancyLightingMod._preventTileParticles;
            _drawingTileEntities = true;
            FancyLightingMod._preventTileParticles = true;
            try
            {
                Main.instance.TilesRenderer.PostDrawTiles(false, false, false);
                Main.instance.TilesRenderer.PostDrawTiles(true, false, false);
            }
            finally
            {
                FancyLightingMod._preventTileParticles = prevPreventTileParticles;
                _drawingTileEntities = false;
            }
        }

        var radius = PreferencesConfig.Instance.AmbientOcclusionRadius;
        var power = PreferencesConfig.Instance.AmbientOcclusionPower();
        var mult = PreferencesConfig.Instance.AmbientOcclusionMult();

        var blurTarget = _blurRenderer.Blur(_ambientOcclusionTarget, null, radius);

        effect = power == 2f ? _toneCurveDefaultEffect : _toneCurveEffect;
        effect.SetParameter("BlurPower", power).SetParameter("BlurMult", mult);
        Blitter.Blit(
            blurTarget,
            _ambientOcclusionTarget,
            effect,
            samplerState: SamplerState.LinearClamp
        );

        return _ambientOcclusionTarget;
    }

    internal RenderTarget2D ApplyAmbientOcclusion(
        RenderTarget2D wallTarget = null,
        bool doDraw = true,
        bool updateWallTarget = true
    )
    {
        if (!LightingConfig.Instance.AmbientOcclusionEnabled())
        {
            return null;
        }

        if (updateWallTarget)
        {
            doDraw = true;
        }

        if (doDraw)
        {
            wallTarget ??= Main.instance.wallTarget;
        }

        TextureUtils.MakeSize(
            ref _blurTarget,
            Main.instance.tileTarget.Width,
            Main.instance.tileTarget.Height,
            SurfaceFormat.Color // SurfaceFormat.Alpha8 is not supported
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
            wallTarget,
            Main.instance.tileTarget,
            Main.instance.tile2Target,
            Main.sceneTilePos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
            Main.sceneTile2Pos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
            _blurTarget,
            doDraw,
            _drawTarget
        );

        if (updateWallTarget)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(wallTarget);
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

        return updateWallTarget ? wallTarget : target;
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
            ref _blurTarget,
            screenTarget.Width,
            screenTarget.Height,
            SurfaceFormat.Color // SurfaceFormat.Alpha8 is not supported
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
            _blurTarget,
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
                MainGraphics.ResetSavedTextures();

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
                    MainGraphics.SetTexture(5, lightedGlow, SamplerState.PointClamp);
                }

                MainGraphics.SetTexture(4, glow, SamplerState.PointClamp);
                Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
                MainGraphics.RestoreSavedTextures();
            }
            Main.spriteBatch.End();
        }

        // Reset special tile counts
        Main.instance.TilesRenderer.PreDrawTiles(false, false, true);
        Main.instance.TilesRenderer.PreDrawTiles(true, false, true);

        return doDraw ? null : target;
    }

    private RenderTarget2D ApplyAmbientOcclusionInner(
        RenderTarget2D wallTarget,
        RenderTarget2D tileTarget,
        RenderTarget2D tile2Target,
        Vector2 tileTargetPosition,
        Vector2 tile2TargetPosition,
        RenderTarget2D target,
        bool doDraw,
        RenderTarget2D drawTarget
    )
    {
        var drawNonSolidTiles = LightingConfig.Instance.DoNonSolidAmbientOcclusion;
        var drawTileEntities = LightingConfig.Instance.DoTileEntityAmbientOcclusion;

        if (!(drawNonSolidTiles || drawTileEntities))
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(target);
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

                var prevPreventTileParticles = FancyLightingMod._preventTileParticles;
                _drawingTileEntities = true;
                FancyLightingMod._preventTileParticles = true;
                try
                {
                    Main.instance.TilesRenderer.PostDrawTiles(false, false, false);
                    Main.instance.TilesRenderer.PostDrawTiles(true, false, false);
                }
                finally
                {
                    FancyLightingMod._preventTileParticles = prevPreventTileParticles;
                    _drawingTileEntities = false;
                    Main.GameViewMatrix.Zoom = currentZoom;
                    Main.screenPosition = currentScreenPosition;
                }
            }

            Main.graphics.GraphicsDevice.SetRenderTarget(target);
            Main.graphics.GraphicsDevice.Clear(Color.White);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                CustomBlendStates.Multiply,
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

        var radius = PreferencesConfig.Instance.AmbientOcclusionRadius;
        var power = PreferencesConfig.Instance.AmbientOcclusionPower();
        var mult = PreferencesConfig.Instance.AmbientOcclusionMult();

        var blurTarget = _blurRenderer.Blur(target, null, radius, false);

        var shader = power == 2f ? _toneMappingDefaultShader : _toneMappingShader;

        Main.graphics.GraphicsDevice.SetRenderTarget(target);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        shader.SetParameter("BlurPower", power).SetParameter("BlurMult", mult).Apply();
        Main.spriteBatch.Draw(
            blurTarget,
            Vector2.Zero,
            null,
            Color.White,
            0f,
            Vector2.Zero,
            new Vector2(
                (float)target.Width / blurTarget.Width,
                (float)target.Height / blurTarget.Height
            ),
            SpriteEffects.None,
            0f
        );
        Main.spriteBatch.End();

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
            CustomBlendStates.MultiplyColorByAlpha,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

        return drawTarget;
    }
}
