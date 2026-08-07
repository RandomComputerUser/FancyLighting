using FancyLighting.VFX;

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
        RenderTarget2D tile2Target,
        bool tileEntityShadows,
        bool cameraMode
    )
    {
        var tile2Shadows = tile2Target is not null;

        if (cameraMode)
        {
            // This code is adapted from vanilla

            Main.instance.TilesRenderer.SpecificHacksForCapture();
            Main.instance.TilesRenderer.PreDrawTiles(true, false, true);
            Main.instance.TilesRenderer.PreDrawTiles(false, false, true);

            var biome = MainGraphics.CameraModeBiome;
            var waterStyleOverride = biome is null
                ? -1
                : (Main.bloodMoon ? 9 : biome.WaterStyle);

            if (tile2Shadows)
            {
                Main.graphics.GraphicsDevice.SetRenderTarget(tile2Target);
                Main.graphics.GraphicsDevice.Clear(Color.Transparent);
                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
                Main.instance.TilesRenderer.Draw(false, false, false, waterStyleOverride);
                Main.tileBatch.End();
                Main.spriteBatch.End();
            }

            Main.graphics.GraphicsDevice.SetRenderTarget(tileTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            Main.instance.TilesRenderer.Draw(true, false, false, waterStyleOverride);
            Main.tileBatch.End();
            Main.spriteBatch.End();
        }

        TextureUtils.MakeSize(
            ref _ambientOcclusionTarget,
            wallTarget.Width,
            wallTarget.Height,
            SurfaceFormat.Color
        );

        var effect = tile2Target is null ? _tilesEffect : _tilesAndTiles2Effect;
        MainGraphics.ResetSavedTextures();

        if (cameraMode)
        {
            effect.SetParameter("MatrixTransform", Matrix.Identity);
            if (tile2Target is not null)
            {
                effect.SetParameter("MatrixTransform2", Matrix.Identity);
                MainGraphics.SetTexture(8, tile2Target, SamplerState.PointClamp);
            }
        }
        else
        {
            var wallTexturePosition = TexturePosition.GetTileTargetPosition(wallTarget);
            wallTexturePosition.TextureToWorldTransform(out var wallToWorldTransform);

            {
                var tileTexturePosition = TexturePosition.GetTileTargetPosition(
                    tileTarget,
                    Main.sceneTilePos
                );
                tileTexturePosition.WorldToTextureTransform(out var tileMatrixTransform);
                Matrix.Multiply(
                    ref wallToWorldTransform,
                    ref tileMatrixTransform,
                    out tileMatrixTransform
                );
                effect.SetParameter("MatrixTransform", tileMatrixTransform);
            }

            if (tile2Shadows)
            {
                var tile2TexturePosition = TexturePosition.GetTileTargetPosition(
                    tile2Target,
                    Main.sceneTile2Pos
                );
                tile2TexturePosition.WorldToTextureTransform(
                    out var tile2MatrixTransform
                );
                Matrix.Multiply(
                    ref wallToWorldTransform,
                    ref tile2MatrixTransform,
                    out tile2MatrixTransform
                );
                effect.SetParameter("MatrixTransform2", tile2MatrixTransform);

                MainGraphics.SetTexture(8, tile2Target, SamplerState.PointClamp);
            }
        }

        Blitter.Blit(tileTarget, _ambientOcclusionTarget, effect);
        MainGraphics.RestoreSavedTextures();

        if (tileEntityShadows)
        {
            var currentScreenPosition = Main.screenPosition;
            var currentMatrixZoom = Main.GameViewMatrix.Zoom;
            var currentMatrixEffects = Main.GameViewMatrix.Effects;
            var currentRasterizerState = Main.Rasterizer;

            var prevPreventTileParticles = FancyLightingMod._preventTileParticles;
            _drawingTileEntities = true;
            FancyLightingMod._preventTileParticles = true;
            try
            {
                if (!cameraMode)
                {
                    Main.screenPosition -= new Vector2(Main.offScreenRange);
                    Main.GameViewMatrix.Zoom = Vector2.One;
                    Main.GameViewMatrix.Effects = SpriteEffects.None;
                    Main.Rasterizer = RasterizerState.CullNone;
                }

                SpriteBatchEffectLoader.Apply(_tileEntityEffect);
                SpriteBatchEffectLoader.Apply(CustomBlendStates.MinAlpha);
                Main.instance.TilesRenderer.PostDrawTiles(false, false, false);
                SpriteBatchEffectLoader.Reset();
            }
            finally
            {
                _drawingTileEntities = false;
                FancyLightingMod._preventTileParticles = prevPreventTileParticles;

                Main.Rasterizer = currentRasterizerState;
                Main.GameViewMatrix.Effects = currentMatrixEffects;
                Main.GameViewMatrix.Zoom = currentMatrixZoom;
                Main.screenPosition = currentScreenPosition;
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
}
