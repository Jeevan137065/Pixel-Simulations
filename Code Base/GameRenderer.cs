using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using Pixel_Simulations.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    public class GameRenderer
    {
        private readonly GameState _state;
        private readonly EntityManager _entityManager;
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
        public GameRenderer(GameState state, EntityManager entityManager, RenderPipeline pipeline, int width, int height)
        {
            _state = state;
            _entityManager = entityManager;
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
            _state.GameCamera.Setcamera(_pipeline.NativeRect, _pipeline.SimRect, _pipeline.FinalRect);
            _state.GameCamera.Follow(_state.Player.Position, 1f);
            var grassAreas = new List<RectangleF>();
            var grassEntities = _entityManager.GetByTag(100);
            _entityManager.LoadFromMap(_state.CurrentMap, _state.PrefabManager, _state, gd);

            _state.Assets.LoadAtlas("emotes", AtlasType.Universal);

            // Initialize the HUD with the font and emote texture
            HUD.LoadContent(_state.Assets.customFont, _state.Assets.GetAtlas("emotes"),gd);
        }
        public void update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var frame = _state._fKeyPressedLastFrame;
            // --- NEW: HUD TESTING INPUTS ---
            if (keyboardState.IsKeyDown(Keys.M) && !frame)
            {
                // Rich Text test! Random colors and text.
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
                // Spawn a random emote from a 4-column, 11-row spritesheet
                Random rand = new Random();
                HUD.SpawnEmote(column: rand.Next(4), row: rand.Next(11), scale: 2.0f);
            }
            if (keyboardState.IsKeyDown(Keys.N) && !frame)
            {
                // Title Drop Test
                HUD.ShowTitleDrop("THE FORGOTTEN RUINS", "Danger Level: High", TitleMode.TopRight);
            }

            // Update debounce flag
            frame = keyboardState.IsKeyDown(Keys.M) || keyboardState.IsKeyDown(Keys.N) || keyboardState.IsKeyDown(Keys.B) || keyboardState.IsKeyDown(Keys.K);

            // Update the HUD internal timers
            HUD.Update(gameTime);
        }
        private void BuildReflectionCache()
        {
            _reflectionRegions.Clear();

            // Query the fast dictionary for exactly what we need!
            var reflectionEntities = _entityManager.GetByTag(10);

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
            // ==========================================
            // 0. LOGIC PRE-PASSES
            // ==========================================
            Vector2 simRes = new Vector2(_pipeline.SimRect.Width, _pipeline.SimRect.Height);

            _state.Shaders.UpdateParticles(gameTime, _state.Weather, _state.GameCamera.Position, simRes);

            // This grabs the interpolated TimeOfDay color from the JSON DB!
            _state.Shaders.UpdatePostProcessing(_state.Weather, _state.TimeSystem, gameTime);

            _entityManager.UpdateRenderList(_state);

            bool useParallax = _state.DebugPool[GameBool.EnableParallax];
            float strength = _state.ParallaxStrength;
            RectangleF streamBounds = _state.GetStreamingBounds(_pipeline.NativeRect.Width, _pipeline.NativeRect.Height);
            // ==========================================
            // PASS 1: MULTI-CHANNEL DEPTH GENERATION
            // ==========================================
            _pipeline.Begin(RenderLayer.VolumeDepth, Color.Transparent);
            _depthRenderer.DrawChunks(spriteBatch, _state.TerrainMaskChunks, streamBounds, _state.GameCamera, BlendState.Opaque, Color.White,_state.MaskOffset);
            // DEDICATED DEPTH CALL
            _entityManager.DrawDepthPass(_pipeline._graphicsDevice, _state.GameCamera, _depthRenderer._depthEffect, useParallax, strength);

            // ==========================================
            // PASS 3: ALBEDO BACKGROUND (480p)
            // ==========================================
            _pipeline.Begin(RenderLayer.Albedo, Color.Transparent);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _state.GameCamera.NativeTransform);
            _mapRenderer.Draw(spriteBatch, LayerType.Tile);
            spriteBatch.End();

            // ==========================================
            // PASS 3: TERRAIN NORMALS (FIX A: Draw to Normal RT ONLY)
            // ==========================================
            _pipeline.Begin(RenderLayer.Normal, Color.Transparent);

            _depthRenderer.DrawChunks(spriteBatch, _state.TerrainNormalChunks, streamBounds, _state.GameCamera, BlendState.Opaque, Color.White, _state.MaskOffset);
            
            // ==========================================
            // PASS 4: OBJECTS (MRT: DYNAMIC & NORMAL)
            // ==========================================
            // Now bind BOTH targets for the objects. The shader handles outputting to COLOR0 and COLOR1
            _pipeline._graphicsDevice.SetRenderTargets(
                new RenderTargetBinding(_pipeline.GetRenderTarget(RenderLayer.Dynamic)),
                new RenderTargetBinding(_pipeline.GetRenderTarget(RenderLayer.Normal)));

            _pipeline._graphicsDevice.Clear(Color.Transparent);

            _entityManager.DrawNormalPass(_pipeline._graphicsDevice, _state.GameCamera, _state.Assets, useParallax, strength);
            _entityManager.DrawAlbedoPass(_pipeline._graphicsDevice, _state.GameCamera, useParallax, strength);

            // ==========================================
            // PASS 4: DEPTH-TESTED GRASS
            // ==========================================
            // Grass draws DIRECTLY over objects. Discards blades behind objects using the VolumeDepth map!
            _pipeline._graphicsDevice.BlendState = BlendState.AlphaBlend;
            Texture2D depthTex = _pipeline.GetRenderTarget(RenderLayer.VolumeDepth);
            // We pass the SimWVP matrix to correctly scale and position the grass
            _state.Grass.Draw(_state.GameCamera.NativeWVP, depthTex);
            // ==========================================
            // PASS 6: SHADERS / WATER FLOOD
            // ==========================================
            _pipeline.Begin(RenderLayer.Shader, Color.Transparent);
            _state.Shaders.DrawWeatherOverlays(spriteBatch, _state.GameCamera.Position, new Vector2(_pipeline.SimRect.Width, _pipeline.SimRect.Height), _state.Weather, _pipeline.GetRenderTarget(RenderLayer.VolumeDepth));
            _state.Shaders.DrawParticleWeather(spriteBatch, _state.Weather, _state.GameCamera.SimWVP, (float)gameTime.TotalGameTime.TotalSeconds);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _state.GameCamera.SimTransform);
            spriteBatch.End();
            // ==========================================
            // PASS 5: POST-PROCESS & COMPOSE
            // ==========================================
            _pipeline.PresentFinal(spriteBatch);
            _pipeline.PostFinal(spriteBatch, null, _state.Shaders.GetActivePostProcessingEffects());
            
            spriteBatch.Begin(); // Do not pass any matrix here for HUD
            HUD.Draw(spriteBatch); // <--- Add this right here!
            HUD.DrawInventory(spriteBatch, _state);
            spriteBatch.End();
            
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _state.GameCamera.ScreenTransform);
            HUD.DebugDraw(_state, spriteBatch);
            Debug_Draw(spriteBatch);
            spriteBatch.End();
        }
        public void Debug_Draw(SpriteBatch spriteBatch)
        {
            if (_state.DebugPool[GameBool.ShowCollision] || _state.DebugPool[GameBool.ShowLinks] || _state.DebugPool[GameBool.ShowShapes])
            {


                if (_state.DebugPool[GameBool.ShowCollision])
                {
                    float lineThickness = 1f / _state.GameCamera.Zoom;

                    // 1. Draw Full Sprite Bounds (Cyan)
                    RectangleF fullBoundsb = new RectangleF(_state.Player.Position.X, _state.Player.Position.Y, _state.Player.SourceRect.Width, _state.Player.SourceRect.Height);
                    spriteBatch.DrawRectangle(_state.Player.CollisionBounds, Color.Azure, lineThickness);

                    // 2. Draw Foot Collision Box (Red)
                    spriteBatch.DrawRectangle(_state.Player.FootBounds, Color.Red, lineThickness);

                    // 3. Draw Interaction Box (Yellow)
                    spriteBatch.DrawRectangle(_state.Player.GetInteractionBox(), Color.Yellow, lineThickness);
                }

                // 2. Delegate map debug drawing to the EntityManager!
                _entityManager.DrawDebugOverlays(spriteBatch, _state);

            }
        }

    }
}