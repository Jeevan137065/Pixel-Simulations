using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Pixel_Simulations.Data;
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
        private PixelTexture pt;

        private readonly List<RenderableSprite> _staticSprites = new List<RenderableSprite>();
        private readonly List<RenderableSprite> _drawList = new List<RenderableSprite>();
        private bool _isFirstLoad = true;
        public GameRenderer(GameState state, EntityManager entityManager, RenderPipeline pipeline, int width, int height)
        {
            _state = state;
            _entityManager = entityManager;
            _pipeline = pipeline;
            _mapRenderer = new GameMapRenderer(state, width, height);
            _depthRenderer = new VolumetricDepthRenderer();

            // Force an immediate calculation so the matrices are ready instantly
        }
        public void LoadContent(ContentManager content,GraphicsDevice gd)
        {
            _depthRenderer.LoadContent(gd, content);
            _state.GameCamera.Setcamera(_pipeline.NativeRect, _pipeline.SimRect);
            _state.GameCamera.Follow(_state.Player.Position, 1f);
            pt = new PixelTexture(gd,1);
            var grassAreas = new List<RectangleF>();
            var grassEntities = _entityManager.GetByTag("#Grass");

            // PRE-BUILD STATIC LIST ONCE
            _staticSprites.Clear();
            foreach (var entity in _entityManager.AllEntities)
            {
                if (entity.Prefab != null && entity.IsActive)
                {
                    var tex = _state.Assets.GetAtlas(entity.Prefab.AtlasName);
                    if (tex != null)
                    {
                        _staticSprites.Add(new RenderableSprite
                        {
                            Texture = tex,
                            Position = entity.Position,
                            SourceRect = entity.Prefab.SourceRect,
                            Origin = entity.Prefab.Pivot,
                            Scale = Vector2.One,
                            Rotation = 0f,
                            BaseWorldY = entity.Position.Y,
                            DrawDepth = DepthUtil.Calculate(entity.Position.Y)
                        });
                    }
                }
            }
        }
        private void BuildDrawList()
        {
            _drawList.Clear();

            // 1. Instantly copy all static geometry
            _drawList.AddRange(_staticSprites);

            // 2. Add Dynamic Entities (Player, NPCs, etc.)
            _drawList.Add(new RenderableSprite
            {
                Texture = _state.Player.Texture,
                Position = _state.Player.Position,
                SourceRect = _state.Player.SourceRect,
                Origin = _state.Player.Origin,
                Scale = Vector2.One,
                Rotation = 0f,
                BaseWorldY = _state.Player.Position.Y,
                DrawDepth = DepthUtil.Calculate(_state.Player.Position.Y)
            });

            // 3. Sort. (Use true for subsequent frames because the list is mostly sorted)
            SpriteSorter.Sort(_drawList, !_isFirstLoad);
            _isFirstLoad = false;
        }
        public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
        {
            // --- 1. PRE-PASSES (e.g. Grass, Particles) ---
            _state.Shaders.UpdateParticles(gameTime, _state.Weather, _state.GameCamera.Position, new Vector2(_pipeline.SimRect.Width, _pipeline.SimRect.Height));
            _state.Shaders.UpdatePostProcessing(_state.Weather, _state.TimeSystem, gameTime);
            BuildDrawList();
            RectangleF streamBounds = _state.GetStreamingBounds(_pipeline.NativeRect.Width, _pipeline.NativeRect.Height);
            // ==========================================
            // PASS 1: MULTI-CHANNEL DEPTH GENERATION
            // ==========================================
            _pipeline.Begin(RenderLayer.VolumeDepth, Color.Transparent);

            _depthRenderer.DrawTerrainDepth(spriteBatch, _state.TerrainMaskChunks, streamBounds, _state.GameCamera);

            // B. Volume Altitude (Red Channel)
            _depthRenderer.BeginVolumePass(spriteBatch, _state.GameCamera);
            foreach (var sprite in _drawList)
            {
                _depthRenderer.DrawVolumetricSprite(spriteBatch, sprite);
            }
            _depthRenderer.EndPass(spriteBatch);

            // ==========================================
            // PASS 2: ALBEDO (NativeFinal Matrix - 480p)
            // ==========================================
            _pipeline.Begin(RenderLayer.Albedo, Color.CornflowerBlue);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _state.GameCamera.NativeFinal);
            _mapRenderer.Draw(spriteBatch, LayerType.Tile);
            spriteBatch.End();
            RenderTarget2D depthTarget = _pipeline.GetRenderTarget(RenderLayer.VolumeDepth);
            // ==========================================
            // PASS 2: ALBEDO (Background)
            _pipeline.Begin(RenderLayer.Albedo, Color.CornflowerBlue);

            // 2A. Draw the normal Tilemap
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _state.GameCamera.NativeFinal);
            _mapRenderer.Draw(spriteBatch, LayerType.Tile);
            spriteBatch.End();

            // 2B. Draw the Water Flood!
            // We use the WaterFlood shader. We draw the DepthTarget over the entire 480p screen.
            // The shader converts any low-elevation blue pixels into translucent water!
            var waterEffect = _state.Shaders.Effects["WaterFlood"];
            if (waterEffect != null)
            {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, waterEffect);
                // Draw the 960p depth target, scaling it down to fit the 480p Albedo target exactly
                spriteBatch.Draw(depthTarget, _pipeline.NativeRect, Color.White);
                spriteBatch.End();
            }

            // ==========================================
            // PASS 3: DYNAMIC ALBEDO (Props, Player, Grass)
            // ==========================================
            _pipeline.Begin(RenderLayer.Dynamic, Color.Transparent);

            // Grass calculates its own view-projection internally
            Matrix grassWVP = _state.GameCamera.SimFinal * Matrix.CreateOrthographicOffCenter(0, _pipeline.SimRect.Width, _pipeline.SimRect.Height, 0, 0, 1);
            _state.Grass.Draw(grassWVP);

            spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, _state.GameCamera.SimFinal);

            // Draw Player (Using the new properties!)
            float pDepth = DepthUtil.Calculate(_state.Player.Position.Y);
            spriteBatch.Draw(_state.Player.Texture, _state.Player.Position, _state.Player.SourceRect, Color.White, 0f, _state.Player.Origin, 1f, SpriteEffects.None, pDepth);

            // Draw Entities via EntityManager
            foreach (var entity in _entityManager.AllEntities)
            {
                if (entity.Prefab != null && entity.IsActive)
                {
                    var tex = _state.Assets.GetAtlas(entity.Prefab.AtlasName);
                    // Calculates depth based on the physical Y coordinate of the bottom of the sprite
                    float entityDepth = DepthUtil.Calculate(entity.Position.Y);
                    spriteBatch.Draw(tex, entity.Position, entity.Prefab.SourceRect, Color.White, 0f, entity.Prefab.Pivot, 1f, SpriteEffects.None, entityDepth);
                }
            }
            spriteBatch.End();

            // ==========================================
            // PASS 4: SHADERS & OVERLAYS (SimFinal Matrix)
            // ==========================================
            _pipeline.Begin(RenderLayer.Shader, Color.Transparent);
            _state.Shaders.DrawWeatherOverlays(spriteBatch, _state.GameCamera.Position, new Vector2(_pipeline.SimRect.Width, _pipeline.SimRect.Height), _state.Weather, depthTarget);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _state.GameCamera.SimFinal);
            _state.Shaders.DrawParticleWeather(spriteBatch, _state.Weather, _state.GameCamera.SimFinal, (float)gameTime.TotalGameTime.TotalSeconds);
            spriteBatch.End();
            // ==========================================
            // PASS 5: POST-PROCESS & COMPOSE
            // ==========================================
            _pipeline.PresentFinal(spriteBatch);
            _pipeline.PostFinal(spriteBatch, null, _state.Shaders.GetActivePostProcessingEffects());
        }
    }
}