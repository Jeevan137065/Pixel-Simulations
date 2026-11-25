using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.ECS;
using Pixel_Simulations.Data;
using Pixel_Simulations.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    public class GameRenderer
    {
        PixelTexture pt;
        private readonly GameState _state;

        private readonly RenderPipeline _pipeline;
        private readonly GameMapRenderer _mapRenderer;
        private readonly VolumetricDepthRenderer _depthRenderer;
        private ParallaxRenderer _parallaxRenderer;
        public HUDManager HUD;
        private GraphicsDevice _gd;
        private readonly List<CachedRenderRegion> _reflectionRegions = new List<CachedRenderRegion>();
        private readonly BlendState WriteBlue = new BlendState
        {
            ColorWriteChannels = ColorWriteChannels.Blue,
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.Zero
        };
        private BasicEffect _debugPolyEffect;
        public GameRenderer(GameState state,  RenderPipeline pipeline, int width, int height)
        {
            _state = state;
           
            _pipeline = pipeline;
            _mapRenderer = new GameMapRenderer(state, width, height);
            _depthRenderer = new VolumetricDepthRenderer();
            HUD = new HUDManager();
            // Force an immediate calculation so the matrices are ready instantly
        }
        public void LoadContent(ContentManager content, GraphicsDevice gd)
        {
            _gd = gd;
            _depthRenderer.LoadContent(gd, content);
            _parallaxRenderer = new ParallaxRenderer(gd, _state.Shaders.Effects["ParallaxSprite"]);
            pt = new PixelTexture(gd, 1);
            _state.GameCamera.Setcamera(_pipeline.NativeRect, _pipeline.SimRect, _pipeline.FinalRect);
            
            var grassAreas = new List<RectangleF>();
            var grassEntities = _state._entityManager.GetByTag(SystemTags.VEGETATION);
            _state._entityManager.LoadFromMap(_state.CurrentMap, _state.PrefabManager, _state, gd);

            _state.Assets.LoadAtlas("emotes", AtlasType.Universal);

            // Initialize the HUD with the font and emote texture
            HUD.LoadContent(_state.Assets.customFont, _state.Assets.GetAtlas("emotes"),gd);
        }
        public void update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            bool isAlt = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt);
            var frame = _state._fKeyPressedLastFrame;
            _state.GameCamera.Follow(_state.Player.Position, 1f, gameTime);

            // Block HUD test triggers when holding Alt (so Alt+M / Alt+B don't collide)
            if (!isAlt)
            {
                if (keyboardState.IsKeyDown(Keys.M) && !frame)
                {
                    string[] testMessages = {
                        "System: Save <c:Green>Completed</c>.",
                        "You picked up <c:Yellow>15x Gold</c>!",
                        "Warning: <c:Red>Low Health!</c>",
                        "Discovered a <c:Purple>Mysterious Artifact</c>."
                    };
                    string randomMsg = testMessages[new Random().Next(testMessages.Length)];
                    HUD.AddMessage(randomMsg, duration: 4f, scale: 1.0f);
                }
                if (keyboardState.IsKeyDown(Keys.B) && !frame)
                {
                    Random rand = new Random();
                    HUD.SpawnEmote(column: rand.Next(4), row: rand.Next(11), scale: 2.0f);
                }
                if (keyboardState.IsKeyDown(Keys.N) && !frame)
                {
                    HUD.ShowTitleDrop("THE FORGOTTEN RUINS", "Danger Level: High", TitleMode.TopRight);
                }
            }

            frame = keyboardState.IsKeyDown(Keys.M) || keyboardState.IsKeyDown(Keys.N) || keyboardState.IsKeyDown(Keys.B) || keyboardState.IsKeyDown(Keys.K);
            HUD.Update(gameTime);
        }
        private void BuildReflectionCache()
        {
            _reflectionRegions.Clear();

            // Query the fast dictionary for exactly what we need!
            var reflectionEntities = _state._entityManager.GetByTag(SystemTags.REFLECTION);

            foreach (var entity in reflectionEntities)
            {
                var baseData = entity.BaseData;

                // 1. Parse Properties safely using your helper
                Color tint = Color.DeepSkyBlue * 0.5f;
                string tintStr = entity.GetProperty("Tint", "180,180,180,128");
                var p = tintStr.Split(',');
                if (p.Length == 4) tint = new Color(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]));

                var region = new CachedRenderRegion
                {
                    ID = baseData.ID,
                    Tint = tint,
                    IsNegative = entity.GetProperty("Direction", "True") == "True",
                    Blur = float.Parse(entity.GetProperty("Blur", "1.5")),
                    RippleSpeed = float.Parse(entity.GetProperty("RippleSpeed", "0.5")),
                    Offset = float.Parse(entity.GetProperty("Offset", "0.0")),
                    PrimitiveType = PrimitiveType.TriangleList
                };

                // 2. Generate GPU Geometry based on the Object Type!
                if (baseData is ShapeObject shapeObj && shapeObj.Shape.Vertices.Count >= 3)
                {
                    region.Bounds = shapeObj.Shape.GetBounds();
                    var verts = shapeObj.Shape.Vertices;
                    region.PrimitiveCount = verts.Count - 2;
                    region.Vertices = new VertexPositionColor[region.PrimitiveCount * 3];

                    // Triangle Fan Triangulation
                    int vIdx = 0;
                    for (int i = 1; i < verts.Count - 1; i++)
                    {
                        region.Vertices[vIdx++] = new VertexPositionColor(new Vector3(verts[0].X, verts[0].Y, 0), Color.White);
                        region.Vertices[vIdx++] = new VertexPositionColor(new Vector3(verts[i].X, verts[i].Y, 0), Color.White);
                        region.Vertices[vIdx++] = new VertexPositionColor(new Vector3(verts[i + 1].X, verts[i + 1].Y, 0), Color.White);
                    }
                }
                else if (baseData is RectangleObject rectObj)
                {
                    region.Bounds = new RectangleF(rectObj.Position, rectObj.Size);
                    region.PrimitiveCount = 2; // 2 triangles make a quad
                    region.Vertices = new VertexPositionColor[6];

                    float l = rectObj.Position.X;
                    float r = rectObj.Position.X + rectObj.Size.X;
                    float t = rectObj.Position.Y;
                    float b = rectObj.Position.Y + rectObj.Size.Y;

                    // Triangle 1 (Top-Left, Top-Right, Bottom-Left)
                    region.Vertices[0] = new VertexPositionColor(new Vector3(l, t, 0), Color.White);
                    region.Vertices[1] = new VertexPositionColor(new Vector3(r, t, 0), Color.White);
                    region.Vertices[2] = new VertexPositionColor(new Vector3(l, b, 0), Color.White);

                    // Triangle 2 (Top-Right, Bottom-Right, Bottom-Left)
                    region.Vertices[3] = new VertexPositionColor(new Vector3(r, t, 0), Color.White);
                    region.Vertices[4] = new VertexPositionColor(new Vector3(r, b, 0), Color.White);
                    region.Vertices[5] = new VertexPositionColor(new Vector3(l, b, 0), Color.White);
                }
                else if (baseData is PointObject pointObj)
                {
                    // For a circle, we generate a highly-tessellated polygon!
                    int segments = 32;
                    region.Bounds = new RectangleF(pointObj.Position.X - pointObj.Radius, pointObj.Position.Y - pointObj.Radius, pointObj.Radius * 2, pointObj.Radius * 2);
                    region.PrimitiveCount = segments;
                    region.Vertices = new VertexPositionColor[segments * 3];

                    float angleStep = MathHelper.TwoPi / segments;
                    Vector3 center = new Vector3(pointObj.Position.X, pointObj.Position.Y, 0);

                    int vIdx = 0;
                    for (int i = 0; i < segments; i++)
                    {
                        float a1 = i * angleStep;
                        float a2 = (i + 1) * angleStep;

                        Vector3 p1 = center + new Vector3((float)System.Math.Cos(a1) * pointObj.Radius, (float)System.Math.Sin(a1) * pointObj.Radius, 0);
                        Vector3 p2 = center + new Vector3((float)System.Math.Cos(a2) * pointObj.Radius, (float)System.Math.Sin(a2) * pointObj.Radius, 0);

                        region.Vertices[vIdx++] = new VertexPositionColor(center, Color.White);
                        region.Vertices[vIdx++] = new VertexPositionColor(p1, Color.White);
                        region.Vertices[vIdx++] = new VertexPositionColor(p2, Color.White);
                    }
                }

                // Only add valid regions
                if (region.PrimitiveCount > 0 && region.Vertices != null)
                {
                    _reflectionRegions.Add(region);
                }
            }
        }

      

        public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
        {
            bool useParallax = _state.DebugPool[GameBool.EnableParallax];
            float strength = _state.ParallaxStrength;
            RectangleF streamBounds = _state.GetStreamingBounds(_pipeline.NativeRect.Width, _pipeline.NativeRect.Height);

            // Step 1: Pre-Logic & Simulation Buffering
            ExecutePreLogicAndSimulation(gameTime);

            // Step 2: Depth, Height & Water Data Generation
            GenerateDepthAndWaterDataBuffers(spriteBatch,streamBounds, strength, useParallax);

            // Step 3: Albedo, Geometry & Normal Buffer Passes
            RenderAlbedoGeometryAndNormalPasses(spriteBatch,gameTime, streamBounds, strength, useParallax);

            // Step 4: Light Masks, Shaders & Atmospheric Overlays
            RenderLightMaskAndAtmosphericOverlays(gameTime, spriteBatch);

            // Step 5: Composition, Presentation, HUD & Debug UI
            CompositeAndPresentFinalFrame(gameTime, spriteBatch);

        }

        private void ExecutePreLogicAndSimulation(GameTime gameTime)
        {
            Vector2 simRes = new Vector2(_pipeline.SimRect.Width, _pipeline.SimRect.Height);

            // Compute weather climate vector fields
            _state.Shaders.UpdateParticles(gameTime, _state.Weather, _state.GameCamera.Position, simRes);
            _state.Shaders.UpdatePostProcessing(_state.Weather, _state.TimeSystem, gameTime);

            // Compile dynamic render lists
            _state._entityManager.UpdateRenderList(_state);
        }

        private void GenerateDepthAndWaterDataBuffers(SpriteBatch spriteBatch, RectangleF streamBounds, float strength, bool useParallax)
        {
            Effect zShader = _state.Shaders.Effects["ZDepthShader"];

            // 1. Water Surface / Wave Data
            _pipeline.Begin(RenderLayer.WaterData, Color.Transparent, _state.GameCamera);
            _depthRenderer.DrawChunks(spriteBatch, _state.TerrainWaterChunks, streamBounds, _state.GameCamera, BlendState.Opaque, Color.White, _state.MaskOffset);

            // 2. Y-DEPTH: R = Volume Height | G = 0 | B = Elevation Mask
            // Note: Cleared to 0 (ground level altitude). TerrainMaskChunks is NOT drawn here to avoid corrupting Red with Biome IDs!
            _pipeline.Begin(RenderLayer.YDepth, Color.Transparent, _state.GameCamera);
            _depthRenderer.DrawMaskElevationToYDepth(spriteBatch, _state.TerrainMaskChunks, streamBounds, _state.GameCamera);
            _state._entityManager.DrawYDepthPass(_pipeline._graphicsDevice, _state.GameCamera, useParallax, strength, _state._entityManager._drawList);

            // 3. Z-DEPTH: R = Z Sorting Depth | G = Spawns | B = Biomes
            _pipeline.Begin(RenderLayer.ZDepth, Color.Transparent, _state.GameCamera);
            _depthRenderer.DrawMaskSpawnsAndBiomesToZDepth(spriteBatch, _state.TerrainMaskChunks, streamBounds, _state.GameCamera);
            _state._entityManager.DrawZDepthPass(_pipeline._graphicsDevice, _state.GameCamera, zShader, useParallax, strength, _state._entityManager._drawList);
        }

        private void RenderAlbedoGeometryAndNormalPasses(SpriteBatch spriteBatch, GameTime gameTime, RectangleF streamBounds, float strength, bool useParallax)
        {
            var celestial = ModularBuildingRenderer.GetCelestialState(_state.TimeSystem.TimeOfDay);
            var modularBuildings = _state.CurrentMap?.Layers
                .OfType<ObjectLayer>()
                .SelectMany(l => l.Objects.OfType<ModularBuildingInstance>())
                .ToList() ?? new List<ModularBuildingInstance>();

            if (!_state.IsInsideBuilding)
            {
                _pipeline.Begin(RenderLayer.Albedo, Color.Transparent, _state.GameCamera);

                spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _state.GameCamera.NativeTransform);
                _mapRenderer.Draw(spriteBatch, LayerType.Tile);

                foreach (var bldg in modularBuildings)
                {
                    ModularBuildingRenderer.DrawBuildingFloorPlates(spriteBatch, bldg, _state.Assets);
                }
                spriteBatch.End();

                _pipeline.Begin(RenderLayer.Normal, new Color(128, 128, 255, 255), _state.GameCamera);
                _depthRenderer.DrawChunks(spriteBatch, _state.TerrainNormalChunks, streamBounds, _state.GameCamera, BlendState.Opaque, Color.White, _state.MaskOffset);
                _state._entityManager.DrawNormalPass(_pipeline._graphicsDevice, _state.GameCamera, _state.Assets, useParallax, strength, _state._entityManager._drawList);

                spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _state.GameCamera.SimTransform);
                foreach (var bldg in modularBuildings)
                {
                    ModularBuildingRenderer.DrawBuildingNormals(spriteBatch, bldg, _state.Assets);
                }
                spriteBatch.End();

                // Dynamic Layer with multi-strip horizontal wind simulation
                _pipeline.Begin(RenderLayer.Dynamic, Color.Transparent, _state.GameCamera);
                _state._entityManager.DrawAlbedoPass(_pipeline._graphicsDevice, _state.GameCamera, useParallax, strength, _state._entityManager._drawList, _state.Weather, gameTime);

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, _state.GameCamera.SimTransform);
                foreach (var bldg in modularBuildings)
                {
                    ModularBuildingRenderer.DrawBuildingDynamicGame(spriteBatch, bldg, _state.Assets, celestial, _state.Player.Foot.Y);
                }
                spriteBatch.End();

                _state.Grass.Draw(_state.GameCamera, _pipeline.GetRenderTarget(RenderLayer.ZDepth), _state.Weather);
            }
            else
            {
                _pipeline.Begin(RenderLayer.Albedo, new Color(12, 14, 18), _state.GameCamera);

                spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _state.GameCamera.NativeTransform);
                foreach (var bldg in modularBuildings.Where(b => b.IsPlayerInside))
                {
                    ModularBuildingRenderer.DrawBuildingFloorPlates(spriteBatch, bldg, _state.Assets);
                }
                spriteBatch.End();

                _pipeline.Begin(RenderLayer.Dynamic, Color.Transparent, _state.GameCamera);

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, _state.GameCamera.SimTransform);
                foreach (var bldg in modularBuildings.Where(b => b.IsPlayerInside))
                {
                    ModularBuildingRenderer.DrawBuildingDynamicGame(spriteBatch, bldg, _state.Assets, celestial, _state.Player.Foot.Y);
                }
                spriteBatch.End();

                var interiorSprites = _state._entityManager._drawList.Where(s =>
                    s.AtlasName == "Player" ||
                    (_state.ActiveBuildingInterior != null && s.Position == _state.ActiveBuildingInterior.Position)
                ).ToList();

                _state._entityManager.DrawAlbedoPass(_pipeline._graphicsDevice, _state.GameCamera, false, 0f, interiorSprites, _state.Weather, gameTime);
            }
        }
        private void RenderLightMaskAndAtmosphericOverlays(GameTime gameTime, SpriteBatch spriteBatch)
        {
            var celestial = ModularBuildingRenderer.GetCelestialState(_state.TimeSystem.TimeOfDay);

            // 1. Render Celestial Ambient Tint into LightMask
            _pipeline.Begin(RenderLayer.LightMask, Color.Transparent, _state.GameCamera);
            if (!celestial.IsDay || celestial.Ambient < 0.30f)
            {
                // Apply subtle dusk/night tint across the viewport
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                Color nightOverlay = celestial.LightColor * (celestial.IsDay ? 0.12f : 0.35f);
                spriteBatch.FillRectangle(_state.GameCamera.SimViewportRect, nightOverlay);
                spriteBatch.End();
            }

            // 2. Render Atmospheric Weather & Particles (Outside only)
            _pipeline.Begin(RenderLayer.Shader, Color.Transparent, _state.GameCamera);
            if (!_state.IsInsideBuilding)
            {
                _state.Shaders.DrawWeatherOverlays(spriteBatch, _state.GameCamera.Position, new Vector2(_pipeline.SimRect.Width, _pipeline.SimRect.Height), _state.Weather, _pipeline.GetRenderTarget(RenderLayer.YDepth));
                _state.Shaders.DrawParticleWeather(spriteBatch, _state.Weather, _state.GameCamera.SimWVP, (float)gameTime.TotalGameTime.TotalSeconds);
            }
        }
        private void sbFillPolygon(SpriteBatch sb, Polygon poly, Color color)
        {
            if (poly == null || poly.Vertices == null || poly.Vertices.Count < 3) return;

            var verts = poly.Vertices;
            int triangleCount = verts.Count - 2;
            var vertices = new VertexPositionColor[triangleCount * 3];

            int vIdx = 0;
            for (int i = 1; i < verts.Count - 1; i++)
            {
                vertices[vIdx++] = new VertexPositionColor(new Vector3(verts[0].X, verts[0].Y, 0), color);
                vertices[vIdx++] = new VertexPositionColor(new Vector3(verts[i].X, verts[i].Y, 0), color);
                vertices[vIdx++] = new VertexPositionColor(new Vector3(verts[i + 1].X, verts[i + 1].Y, 0), color);
            }

            // Temporarily flush the SpriteBatch to draw 2D colored primitives
            sb.End();

            if (_debugPolyEffect == null)
            {
                _debugPolyEffect = new BasicEffect(_gd)
                {
                    VertexColorEnabled = true,
                    TextureEnabled = false
                };
            }

            _debugPolyEffect.World = Matrix.Identity;
            _debugPolyEffect.View = Matrix.Identity;
            _debugPolyEffect.Projection = _state.GameCamera.ScreenWVP;

            var prevRasterizer = _gd.RasterizerState;
            var prevBlend = _gd.BlendState;

            _gd.RasterizerState = RasterizerState.CullNone;
            _gd.BlendState = BlendState.AlphaBlend;

            foreach (var pass in _debugPolyEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _gd.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, triangleCount);
            }

            _gd.RasterizerState = prevRasterizer;
            _gd.BlendState = prevBlend;

            // Resume SpriteBatch with the exact same camera matrix
            sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _state.GameCamera.ScreenTransform);
        }
        private void CompositeAndPresentFinalFrame(GameTime gameTime, SpriteBatch spriteBatch)
        {
            // 1. PRESENT COMPOSITE WORLD & POST-PROCESSING TO BACKBUFFER
            _pipeline.PresentFinal(spriteBatch, _state.GameCamera);
            _pipeline.PostFinal(spriteBatch, _state.GameCamera, _state.Shaders.GetActivePostProcessingEffects());

            // 2. WORLD-SPACE DEBUG OVERLAYS (Collisions, Footprints, Spawners, Wireframes)
            // Uses ScreenTransform to map World Coordinates -> 1080p Screen Coordinates
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _state.GameCamera.ScreenTransform);
            Debug_Draw(spriteBatch, gameTime);
            spriteBatch.End();

            // 3. SCREEN-SPACE HUD, INVENTORY & MOUSE TOOLTIPS (1080p Screen Space / Identity Matrix)
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            HUD.Draw(spriteBatch);
            HUD.DrawInventory(spriteBatch, _state);
            HUD.DebugDraw(_state, spriteBatch); // Hover entity inspector tooltip
            spriteBatch.End();

            // 4. MAP TRANSITION FADE OVERLAY
            if (_state.WarpManager.IsWarping && _state.WarpManager.FadeAlpha > 0.0f)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                spriteBatch.FillRectangle(_pipeline.FinalRect, Color.Black * _state.WarpManager.FadeAlpha);
                spriteBatch.End();
            }
        }
        // Inside GameRenderer.cs
        public void Debug_Draw(SpriteBatch spriteBatch, GameTime gameTime = null)
        {
            float lineThick = 1f / _state.GameCamera.Zoom;

            // 1. PLAYER INTERACTION & PLACEMENT PREVIEW
            if (!_state.IsInventoryOpen && _state.Player != null)
            {
                var pixelTex = pt?.GetPixelTexture(false);
                if (pixelTex != null)
                {
                    _state.Player.DrawPlacementPreview(spriteBatch, _state, pixelTex);
                }
                _state.Player.DrawTargetCellDebug(spriteBatch, _state.GameCamera, _state.input.MouseWorldPosition);
            }

            // 2. PHYSICAL COLLISION BARRIERS
            if (_state.DebugPool.ContainsKey(GameBool.ShowCollision) && _state.DebugPool[GameBool.ShowCollision])
            {
                if (_state.CurrentMap != null)
                {
                    var font = _state.Assets?.customFont ?? UITheme.DefaultFont;

                    foreach (var bldg in _state.CurrentMap.Layers.OfType<ObjectLayer>().SelectMany(l => l.Objects.OfType<ModularBuildingInstance>()))
                    {
                        var bp = bldg.BlueprintRef;
                        if (bp != null)
                        {
                            RectangleF tight = ModularBuildingRenderer.GetBuildingTightBounds(bp);
                            RectangleF worldBounds = new RectangleF(bldg.Position.X + tight.X, bldg.Position.Y + tight.Y, tight.Width, tight.Height);
                            spriteBatch.DrawRectangle(worldBounds, Color.Cyan, lineThick * 2f);
                        }
                    }

                    foreach (var entity in _state._entityManager.AllEntities)
                    {
                        if (entity.Prefab != null && entity.GetProperty("Type") == "Building")
                        {
                            RectangleF fBounds = new RectangleF(
                                entity.Position.X - entity.Prefab.Pivot.X,
                                entity.Position.Y - entity.Prefab.Pivot.Y,
                                entity.Prefab.CanvasSize.X,
                                entity.Prefab.CanvasSize.Y
                            );
                            spriteBatch.DrawRectangle(fBounds, Color.Orange, lineThick * 2f);
                        }
                    }
                }

                if (_state.Physics != null && _state.Physics._barriers != null)
                {
                    foreach (var barrier in _state.Physics._barriers)
                    {
                        if (barrier.IsOneWay)
                        {
                            spriteBatch.FillRectangle(barrier.Bounds, Color.Gold * 0.35f);
                            spriteBatch.DrawRectangle(barrier.Bounds, Color.Gold, lineThick * 2);
                        }
                        else
                        {
                            spriteBatch.FillRectangle(barrier.Bounds, Color.Red * 0.3f);
                            spriteBatch.DrawRectangle(barrier.Bounds, Color.Red, lineThick);
                        }
                    }
                }

                if (_state.Player != null)
                {
                    spriteBatch.FillRectangle(_state.Player.FootBounds, Color.LimeGreen * 0.4f);
                    spriteBatch.DrawRectangle(_state.Player.FootBounds, Color.LimeGreen, lineThick * 2);
                    spriteBatch.DrawRectangle(_state.Player.GetInteractionBox(), Color.Yellow, lineThick);
                }
            }

            // 3. CONTROL NODES & TRIGGERS
            if (_state.DebugPool.ContainsKey(GameBool.ShowControlNodes) && _state.DebugPool[GameBool.ShowControlNodes])
            {
                if (_state.CurrentMap != null)
                {
                    foreach (var ctrlLayer in _state.CurrentMap.Layers.OfType<ControlLayer>())
                    {
                        if (!ctrlLayer.IsVisible) continue;

                        foreach (var shapeObj in ctrlLayer.Shapes)
                        {
                            Color shapeCol = shapeObj.HasTag(SystemTags.WATER) ? Color.DeepSkyBlue : Color.Magenta;
                            sbFillPolygon(spriteBatch, shapeObj.Shape, shapeCol * 0.25f);
                            var verts = shapeObj.Shape.Vertices;
                            for (int i = 0; i < verts.Count; i++)
                            {
                                spriteBatch.DrawLine(verts[i], verts[(i + 1) % verts.Count], shapeCol, lineThick * 1.5f);
                            }
                        }

                        foreach (var rect in ctrlLayer.Rectangles)
                        {
                            RectangleF rBounds = new RectangleF(rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y);
                            spriteBatch.FillRectangle(rBounds, Color.DarkCyan * 0.25f);
                            spriteBatch.DrawRectangle(rBounds, Color.DarkCyan, lineThick * 2);
                        }

                        foreach (var pt in ctrlLayer.Points)
                        {
                            spriteBatch.DrawCircle(pt.Position, pt.Radius, 24, Color.HotPink, lineThick * 2);
                        }
                    }
                }
            }

            // 4. ANIMATED MULTI-STRIP WIND SLICE BOUNDS DEBUG (ALT+G)
            if (_state.IsDebug(GameBool.ShowGrassDebug) && gameTime != null)
            {
                float totalSec = (float)gameTime.TotalGameTime.TotalSeconds;
                WindWaveSample wind = _state.Weather.SampleWindWave(totalSec, 12.0f);

                foreach (var sprite in _state._entityManager._drawList)
                {
                    if (sprite.HasWindSway)
                    {
                        int stripH = Math.Max(1, sprite.StripHeight);
                        int totalH = sprite.SourceRect.Height;
                        float w = sprite.SourceRect.Width * sprite.Scale.X;
                        float pivotY = sprite.Origin.Y * sprite.Scale.Y;

                        for (int y = 0; y < totalH; y += stripH)
                        {
                            int currentStripH = Math.Min(stripH, totalH - y);
                            float localY = y * sprite.Scale.Y;

                            // Calculate exact animated horizontal displacement matching the vertex batcher
                            float shift = EntityManager.CalculateStripDisplacement(
                                sprite.Position, localY, pivotY,
                                sprite.Stiffness, sprite.WaveLength, sprite.FlutterStr, wind);

                            float top = sprite.Position.Y - (sprite.Origin.Y * sprite.Scale.Y) + localY;
                            float left = sprite.Position.X - (sprite.Origin.X * sprite.Scale.X) + shift;

                            RectangleF animatedSliceRect = new RectangleF(left, top, w, currentStripH * sprite.Scale.Y);

                            // Alpha increases higher up the tree where displacement is greatest
                            float flexRatio = Math.Min(1.0f, Math.Abs(shift) / 10.0f);
                            Color sliceColor = Color.Lerp(Color.Cyan * 0.25f, Color.Gold * 0.85f, flexRatio);

                            spriteBatch.DrawRectangle(animatedSliceRect, sliceColor, lineThick);
                        }
                    }
                }
            }
        }

    }
}