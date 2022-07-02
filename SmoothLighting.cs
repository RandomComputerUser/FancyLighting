﻿using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics.Light;
using Terraria.Graphics.Shaders;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Threading.Tasks;

namespace FancyLighting
{
    class SmoothLighting
    {

        internal Texture2D colors;
        internal Texture2D colorsBackground;
        internal Vector2 colorsPosition;
        internal Vector2 colorsPass1Position;
        internal Vector2 colorsPass2Position;
        internal Rectangle lightMapTileArea;
        internal Rectangle lightMapRenderArea;
        internal Rectangle lightMapPass2RenderArea;
        internal RenderTarget2D surface;
        internal RenderTarget2D surface2;
        internal Vector3[] lights;
        internal Color[] finalLights;

        protected bool[] glowingTiles;
        protected Color[] glowingTileColors;

        protected bool dangersense;
        protected bool spelunker;

        private bool _smoothLightingPositionValid;
        private bool _smoothLightingBackComplete;
        private bool _smoothLightingForeComplete;

        internal TileLightScanner TileLightScannerObj;

        protected FancyLightingMod ModInstance;

        public SmoothLighting(FancyLightingMod mod) {
            TileLightScannerObj = new TileLightScanner();
            ModInstance = mod;

            lightMapTileArea = new Rectangle(0, 0, 0, 0);
            lightMapRenderArea = new Rectangle(0, 0, 0, 0);
            lightMapPass2RenderArea = new Rectangle(0, 0, 0, 0);

            _smoothLightingPositionValid = false;

            glowingTiles = new bool[ushort.MaxValue + 1];
            foreach (ushort id in new ushort[] {
                TileID.Crystals,
                TileID.LavaMoss,
                TileID.LavaMossBrick,
                TileID.ArgonMoss,
                TileID.ArgonMossBrick,
                TileID.KryptonMoss,
                TileID.KryptonMossBrick,
                TileID.XenonMoss,
                TileID.XenonMossBrick,
                TileID.MeteoriteBrick
            }) {
                glowingTiles[id] = true;
            }

            glowingTileColors = new Color[glowingTiles.Length];

            glowingTileColors[TileID.Crystals] = Color.White;

            glowingTileColors[TileID.LavaMoss] = glowingTileColors[TileID.LavaMossBrick]       = new Color(254, 122, 0);
            glowingTileColors[TileID.ArgonMoss] = glowingTileColors[TileID.ArgonMossBrick]     = new Color(254, 92, 186);
            glowingTileColors[TileID.KryptonMoss] = glowingTileColors[TileID.KryptonMossBrick] = new Color(215, 255, 0);
            glowingTileColors[TileID.XenonMoss] = glowingTileColors[TileID.XenonMossBrick]     = new Color(0, 254, 242);

            glowingTileColors[TileID.MeteoriteBrick] = new Color(219, 104, 19);

            dangersense = false;
            spelunker = false;
            
            GameShaders.Misc["FancyLighting:UpscalingSmooth"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Shaders/Upscaling", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "UpscaleSmooth"
                );

            GameShaders.Misc["FancyLighting:UpscalingRegular"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Shaders/Upscaling", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "UpscaleNoFilter"
                );
        }

        internal void Unload()
        {
            surface?.Dispose();
            surface2?.Dispose();
        }

        internal bool IsGlowingTile(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Main.tile.Width || y >= Main.tile.Height) return false;

            // Illuminant Paint
            if (Main.tile[x, y].TileColor == PaintID.IlluminantPaint) return true;

            // Crystal Shards, Gelatin Crystal, Glowing Moss, and Meteorite Brick
            if (glowingTiles[Main.tile[x, y].TileType]) return true;

            // Dangersense Potion
            if (dangersense && Terraria.GameContent.Drawing.TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer)) return true;

            // Spelunker Potion
            if (spelunker && Main.IsTileSpelunkable(x, y)) return true;

            return false;
        }

        internal void BlurLightMap(Vector3[] colors, int width, int height)
        {
            if (lights is null || lights.Length < height * width)
            {
                lights = new Vector3[height * width];
            }

            if (width == 0 || height == 0) return;

            if (FancyLightingMod.BlurLightMap)
            {
                Parallel.For(
                    1,
                    width - 1,
                    new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
                    (x) =>
                    {
                        int i = height * x;
                        for (int y = 1; y < height - 1; ++y)
                        {
                            ++i;

                            lights[i].X = (
                                1 * colors[i - height - 1].X + 2 * colors[i - 1].X + 1 * colors[i + height - 1].X
                              + 2 * colors[i - height].X + 4 * colors[i].X + 2 * colors[i + height].X
                              + 1 * colors[i - height + 1].X + 2 * colors[i + 1].X + 1 * colors[i + height + 1].X
                            ) / 16f;

                            lights[i].Y = (
                                1 * colors[i - height - 1].Y + 2 * colors[i - 1].Y + 1 * colors[i + height - 1].Y
                              + 2 * colors[i - height].Y + 4 * colors[i].Y + 2 * colors[i + height].Y
                              + 1 * colors[i - height + 1].Y + 2 * colors[i + 1].Y + 1 * colors[i + height + 1].Y
                            ) / 16f;

                            lights[i].Z = (
                                1 * colors[i - height - 1].Z + 2 * colors[i - 1].Z + 1 * colors[i + height - 1].Z
                              + 2 * colors[i - height].Z + 4 * colors[i].Z + 2 * colors[i + height].Z
                              + 1 * colors[i - height + 1].Z + 2 * colors[i + 1].Z + 1 * colors[i + height + 1].Z
                            ) / 16f;
                        }
                    }
                );
            }
            else
            {
                Array.Copy(colors, lights, height * width);
            }

            int offset = (width - 1) * height;
            for (int i = 0; i < height; ++i)
            {
                lights[i] = colors[i];
                lights[i + offset] = colors[i + offset];
            }

            int end = (width - 1) * height;
            offset = height - 1;
            for (int i = height; i < end; i += height)
            {
                lights[i] = colors[i];
                lights[i + offset] = colors[i + offset];
            }

            Array.Copy(lights, colors, height * width);

            LightingEngine lightEngine = (LightingEngine)ModInstance.field_activeEngine.GetValue(null);
            lightMapTileArea = (Rectangle)ModInstance.field_workingProcessedArea.GetValue(lightEngine);
            lightMapRenderArea = new Rectangle(0, 0, lightMapTileArea.Height, lightMapTileArea.Width);
            lightMapPass2RenderArea = new Rectangle(0, 0, 16 * lightMapTileArea.Width, 16 * lightMapTileArea.Height);

            _smoothLightingPositionValid = false;
            _smoothLightingBackComplete = false;
            _smoothLightingForeComplete = false;
        }

        protected void GetColorsPosition()
        {
            int xmin = lightMapTileArea.X;
            int ymin = lightMapTileArea.Y;
            int width = lightMapTileArea.Width;
            int height = lightMapTileArea.Height;

            if (width == 0 || height == 0) return;

            colorsPosition = 16f * new Vector2(xmin + width, ymin);
            colorsPass1Position = 16f * new Vector2(width, 0);
            colorsPass2Position = 16f * new Vector2(xmin, ymin);

            _smoothLightingPositionValid = true;
        }

        internal void CalculateSmoothLighting(bool background)
        {
            if (!FancyLightingMod.SmoothLightingEnabled) return;

            if (!_smoothLightingPositionValid)
                GetColorsPosition();

            dangersense = Main.LocalPlayer.dangerSense;
            spelunker = Main.LocalPlayer.findTreasure;

            if (!_smoothLightingPositionValid) return;
            if (Main.tile.Height == 0 || Main.tile.Width == 0) return;

            int xmin = lightMapTileArea.X;
            int ymin = lightMapTileArea.Y;
            int width = lightMapTileArea.Width;
            int height = lightMapTileArea.Height;
            int ymax = ymin + height;

            if (finalLights is null || finalLights.Length < height * width)
            {
                finalLights = new Color[height * width];
            }

            int clampedXmin = Math.Clamp(xmin, 0, Main.tile.Width);
            int clampedXmax = Math.Clamp(xmin + width, 0, Main.tile.Width);
            if (clampedXmax - clampedXmin < 1) return;
            int clampedStart = Math.Clamp(clampedXmin - xmin, 0, width);
            int clampedEnd = Math.Clamp(clampedXmax - clampedXmin, 0, width);
            if (clampedEnd - clampedStart < 1) return;

            int clampedYmin = Math.Clamp(ymin, 0, Main.tile.Height);
            int clampedYmax = Math.Clamp(ymax, 0, Main.tile.Height);
            if (clampedYmax - clampedYmin < 1) return;
            int offset = clampedYmin - ymin;
            if (offset < 0 || offset >= height) return;

            if (background && !_smoothLightingBackComplete)
            {
                Parallel.For(
                    clampedStart,
                    clampedEnd,
                    new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
                    (x1) =>
                    {
                        int i = height * x1 + offset;
                        int x = x1 + xmin;
                        for (int y = clampedYmin; y < clampedYmax; ++y)
                        {
                            // Also see IsGlowingTile

                            // Illuminant Paint
                            if (Main.tile[x, y].WallColor == PaintID.IlluminantPaint)
                            {
                                finalLights[i++] = Color.White;
                                continue;
                            }

                            finalLights[i] = new Color(Lighting.GlobalBrightness * lights[i]);
                            ++i;
                        }
                    }
                );

                if (colorsBackground is null)
                    colorsBackground = new Texture2D(Main.graphics.GraphicsDevice, height, width, false, SurfaceFormat.Color);
                else if (colorsBackground.GraphicsDevice != Main.graphics.GraphicsDevice || (colorsBackground.Width < height || colorsBackground.Height < width))
                    colorsBackground = new Texture2D(Main.graphics.GraphicsDevice, Math.Max(colorsBackground.Width, height), Math.Max(colorsBackground.Height, width), false, SurfaceFormat.Color);

                colorsBackground.SetData(0, lightMapRenderArea, finalLights, 0, height * width);

                _smoothLightingBackComplete = true;
            }
            else if (!_smoothLightingForeComplete)
            {
                Parallel.For(
                    clampedStart,
                    clampedEnd,
                    new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
                    (x1) =>
                    {
                        int i = height * x1 + offset;
                        int x = x1 + xmin;
                        for (int y = clampedYmin; y < clampedYmax; ++y)
                        {
                            // Also see IsGlowingTile

                            // Illuminant Paint
                            if (Main.tile[x, y].TileColor == PaintID.IlluminantPaint)
                            {
                                finalLights[i++] = Color.White;
                                continue;
                            }

                            // Crystal Shards, Gelatin Crystal, Glowing Moss, and Meteorite Brick
                            if (glowingTiles[Main.tile[x, y].TileType])
                            {
                                ref Color glow = ref glowingTileColors[Main.tile[x, y].TileType];
                                finalLights[i].R = Math.Max(finalLights[i].R, glow.R);
                                finalLights[i].G = Math.Max(finalLights[i].G, glow.G);
                                finalLights[i].B = Math.Max(finalLights[i].B, glow.B);
                                ++i;
                                continue;
                            }

                            finalLights[i] = new Color(Lighting.GlobalBrightness * lights[i]);

                            // Dangersense Potion
                            if (dangersense && Terraria.GameContent.Drawing.TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer))
                            {
                                if (finalLights[i].R < (byte)255) finalLights[i].R = (byte)255;
                                if (finalLights[i].G < (byte)50) finalLights[i].G = (byte)50;
                                if (finalLights[i].B < (byte)50) finalLights[i].B = (byte)50;
                            }

                            // Spelunker Potion
                            else if (spelunker && Main.IsTileSpelunkable(x, y))
                            {
                                if (finalLights[i].R < (byte)200) finalLights[i].R = (byte)200;
                                if (finalLights[i].G < (byte)170) finalLights[i].G = (byte)170;
                            }

                            ++i;
                        }
                    }
                );
                
                if (colors is null)
                    colors = new Texture2D(Main.graphics.GraphicsDevice, height, width, false, SurfaceFormat.Color);
                else if (colors.GraphicsDevice != Main.graphics.GraphicsDevice || (colors.Width < height || colors.Height < width))
                    colors = new Texture2D(Main.graphics.GraphicsDevice, Math.Max(colors.Width, height), Math.Max(colors.Height, width), false, SurfaceFormat.Color);

                colors.SetData(0, lightMapRenderArea, finalLights, 0, height * width);

                _smoothLightingForeComplete = true;
            }
        }

        internal void DrawSmoothLighting(RenderTarget2D target, bool background)
        {
            if (!FancyLightingMod.SmoothLightingEnabled) return;
            if (!background && !_smoothLightingForeComplete) return;
            if (background && !_smoothLightingBackComplete) return;

            if (surface is null
                || surface.GraphicsDevice != Main.graphics.GraphicsDevice
                || surface.Width != Main.instance.tileTarget.Width
                || surface.Height != Main.instance.tileTarget.Height)
            {
                surface?.Dispose();
                surface = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
            }

            Texture2D lightMapTexture = background ? colorsBackground : colors;

            if (FancyLightingMod.CustomUpscalingEnabled)
            {
                if (surface2 is null
                    || surface2.GraphicsDevice != Main.graphics.GraphicsDevice
                    || surface2.Width < 16f * lightMapTexture.Height
                    || surface2.Height < 16f * lightMapTexture.Width)
                {
                    surface2?.Dispose();
                    surface2 = new RenderTarget2D(
                        Main.graphics.GraphicsDevice,
                        Math.Max(16 * lightMapTexture.Height, surface2?.Width ?? 0),
                        Math.Max(16 * lightMapTexture.Width, surface2?.Height ?? 0)
                    );
                }

                Main.instance.GraphicsDevice.SetRenderTarget(surface2);

                Main.spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );

                GameShaders.Misc["FancyLighting:UpscalingSmooth"]
                    .UseShaderSpecificData(new Vector4(0.5f / lightMapTexture.Width, lightMapTexture.Width, 1f / lightMapTexture.Width, 0f))
                    .Apply(null);
                Main.spriteBatch.Draw(
                    lightMapTexture,
                    colorsPass1Position,
                    lightMapRenderArea,
                    Color.White,
                    (float)(Math.PI / 2.0),
                    Vector2.Zero,
                    16f,
                    SpriteEffects.FlipVertically,
                    0f
                );

                Main.spriteBatch.End();

                Main.instance.GraphicsDevice.SetRenderTarget(surface);
                Main.instance.GraphicsDevice.Clear(Color.White);

                Main.spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    FancyLightingMod.MultiplyBlend,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );

                GameShaders.Misc["FancyLighting:UpscalingSmooth"]
                    .UseShaderSpecificData(new Vector4(0.5f / lightMapTexture.Height, lightMapTexture.Height, 1f / lightMapTexture.Height, 0f))
                    .Apply(null);
                Main.spriteBatch.Draw(
                    surface2,
                    colorsPass2Position - (Main.screenPosition - new Vector2(Main.offScreenRange)),
                    lightMapPass2RenderArea,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    1f,
                    SpriteEffects.None,
                    0f
                );
                GameShaders.Misc["FancyLighting:UpscalingRegular"]
                    .Apply(null);
            }
            else
            {
                Main.instance.GraphicsDevice.SetRenderTarget(surface);
                Main.instance.GraphicsDevice.Clear(Color.White);

                Main.spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    FancyLightingMod.MultiplyBlend
                );
                Main.spriteBatch.Draw(
                    lightMapTexture,
                    colorsPosition - (Main.screenPosition - new Vector2(Main.offScreenRange)),
                    lightMapRenderArea,
                    Color.White,
                    (float)(Math.PI / 2.0),
                    Vector2.Zero,
                    16f,
                    SpriteEffects.FlipVertically,
                    0f
                );
            }

            if (!FancyLightingMod.RenderOnlyLight)
            {
                Main.spriteBatch.Draw(
                    target,
                    Vector2.Zero,
                    null,
                    Color.White,
                    0f,
                    new Vector2(0, 0),
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }

            Main.spriteBatch.End();

            Main.instance.GraphicsDevice.SetRenderTarget(target);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin();
            Main.spriteBatch.Draw(
                surface,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();
            Main.instance.GraphicsDevice.SetRenderTarget(null);
        }

    }
}
