using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    // A runtime wrapper for MapObjects that adds active game state
    public class GameEntity
    {
        public MapObject BaseData { get; set; }
        public ObjectPrefab Prefab { get; set; } // Null if it's a trigger/shape
        public Vector2 Position { get; set; }
        public bool IsActive { get; set; } = true;

        // Helper to grab custom properties easily
        public string GetProperty(string key, string fallback = "")
        {
            if (BaseData.Properties.TryGetValue(key, out var prop)) return prop.Value;
            if (Prefab != null && Prefab.Properties.TryGetValue(key, out var pProp)) return pProp.Value;
            return fallback;
        }
    }
    public class CachedRenderRegion
    {
        public string ID { get; set; }
        public RectangleF Bounds { get; set; } // Used to cull off-screen reflections

        // GPU Drawing Data
        public VertexPositionColor[] Vertices { get; set; }
        public int PrimitiveCount { get; set; }
        public PrimitiveType PrimitiveType { get; set; } // Triangles for shapes/rects
        // Cached Shader Properties
        public Color Tint { get; set; }
        public bool IsNegative { get; set; }
        public float Blur { get; set; }
        public float RippleSpeed { get; set; }
        public float Offset { get; set; }
    }
    public class EntityManager
    {
        private readonly List<GameEntity> _allEntities = new List<GameEntity>();
        private readonly Dictionary<string, GameEntity> _entitiesById = new Dictionary<string, GameEntity>();
        private readonly Dictionary<string, List<GameEntity>> _entitiesByTag = new Dictionary<string, List<GameEntity>>();

        public IReadOnlyList<GameEntity> AllEntities => _allEntities;
        private readonly List<RenderableSprite> _staticSprites = new List<RenderableSprite>();
        private readonly List<RenderableSprite> _drawList = new List<RenderableSprite>();
        private bool _isFirstLoad = true;

        // Hardware Drawing Buffers
        private const int MAX_SPRITES = 4096;
        private VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[MAX_SPRITES * 4];
        private short[] _indices = new short[MAX_SPRITES * 6];
        private BasicEffect _basicEffect;
        public void LoadFromMap(Map map, PrefabManager prefabManager, GameState state, GraphicsDevice gd)
        {
            Clear();

            // 1. Initialize Batcher Indices (Standard Quad Pattern)
            for (int i = 0; i < MAX_SPRITES; i++)
            {
                _indices[i * 6 + 0] = (short)(i * 4 + 0);
                _indices[i * 6 + 1] = (short)(i * 4 + 1);
                _indices[i * 6 + 2] = (short)(i * 4 + 2);
                _indices[i * 6 + 3] = (short)(i * 4 + 1);
                _indices[i * 6 + 4] = (short)(i * 4 + 3);
                _indices[i * 6 + 5] = (short)(i * 4 + 2);
            }

            // 2. Initialize BasicEffect
            if (_basicEffect == null)
            {
                _basicEffect = new BasicEffect(gd)
                {
                    TextureEnabled = true,
                    VertexColorEnabled = true
                };
            }

            // 3. Load Map Objects
            foreach (var layer in map.Layers)
            {
                if (layer is ObjectLayer objLayer)
                    foreach (var obj in objLayer.Objects) RegisterObject(obj, prefabManager);

                if (layer is ControlLayer ctrlLayer)
                {
                    foreach (var rect in ctrlLayer.Rectangles) RegisterObject(rect, prefabManager);
                    foreach (var shape in ctrlLayer.Shapes) RegisterObject(shape, prefabManager);
                    foreach (var pt in ctrlLayer.Points) RegisterObject(pt, prefabManager);
                }
            }

            // 4. Cache Static Sprites for blazing fast rendering
            _staticSprites.Clear();
            foreach (var entity in _allEntities)
            {
                if (entity.Prefab != null && entity.IsActive)
                {
                    var tex = state.Assets.GetAtlas(entity.Prefab.AtlasName);
                    if (tex != null)
                    {
                        // --- AUTOMATIC HEIGHT-BASED PARALLAX ---
                        float spriteHeight = entity.Prefab.SourceRect.Height * (entity.BaseData is PropObject pr ? pr.Scale.Y : 1f);
                        float pMask = 0f;

                        // Start applying parallax if taller than a standard tile (e.g. 32px or 64px)
                        // You mentioned 540 and 1080, but remember your sprites are pixel art!
                        // Adjust these numbers based on your actual sprite pixel heights.
                        float minHeight = 240;  // Bushes, benches (No Parallax)
                        float maxHeight = 520; // Tall Trees, Buildings (Max Parallax)

                        if (spriteHeight > minHeight)
                        {
                            // Smoothly lerp the mask from 0.0 to 1.0 based on height
                            pMask = MathHelper.Clamp((spriteHeight - minHeight) / (maxHeight - minHeight), 0f, 1f);
                        }

                        // --- EDITOR PROPERTY OVERRIDE ---
                        // If the user manually set a 'Parallax' property in the Editor Inspector, use that instead!
                        string manualParallax = entity.GetProperty("Parallax", "");
                        if (!string.IsNullOrEmpty(manualParallax) && float.TryParse(manualParallax, out float val))
                        {
                            pMask = MathHelper.Clamp(val, 0f, 1f);
                        }

                        _staticSprites.Add(new RenderableSprite
                        {
                            AtlasName = entity.Prefab.AtlasName,
                            Texture = tex,
                            Position = entity.Position,
                            SourceRect = entity.Prefab.SourceRect,
                            Origin = entity.Prefab.Pivot,
                            Scale = entity.BaseData is PropObject p ? p.Scale : Vector2.One,
                            Rotation = entity.BaseData is PropObject r ? r.Rotation : 0f,
                            BaseWorldY = entity.Position.Y,
                            ParallaxMask = pMask
                        });
                    }
                }
            }

            _isFirstLoad = true;
        }
        private void RegisterObject(MapObject obj, PrefabManager prefabManager)
        {
            var entity = new GameEntity
            {
                BaseData = obj,
                Position = obj.Position,
                Prefab = (obj is PropObject prop) ? prefabManager.GetPrefab(prop.PrefabID) : null
            };

            _allEntities.Add(entity);
            _entitiesById[obj.ID] = entity;

            // Merge Instance tags and Prefab tags
            var allTags = new HashSet<string>(obj.Tags);
            if (entity.Prefab != null) allTags.UnionWith(entity.Prefab.Tags);

            foreach (var tag in allTags)
            {
                if (!_entitiesByTag.ContainsKey(tag))
                    _entitiesByTag[tag] = new List<GameEntity>();
                _entitiesByTag[tag].Add(entity);
            }
        }
        public void UpdateRenderList(GameState state)
        {
            _drawList.Clear();
            _drawList.AddRange(_staticSprites);

            // Add Dynamic Entities (Player, NPCs)
            if (state.Player != null)
            {
                _drawList.Add(new RenderableSprite
                {
                    AtlasName = "Player",
                    Texture = state.Player.Texture,
                    Position = state.Player.Position,
                    SourceRect = state.Player.SourceRect,
                    Origin = state.Player.Origin,
                    Scale = Vector2.One,
                    Rotation = 0f,
                    BaseWorldY = state.Player.Position.Y,
                    ParallaxMask = 0.0f // PLAYER REMAINS FLAT
                });
            }

            // Sort Back-To-Front based on BaseWorldY
            SpriteSorter.Sort(_drawList, !_isFirstLoad);
            _isFirstLoad = false;
        }

        public void RemoveEntity(string id)
        {
            if (_entitiesById.TryGetValue(id, out var entity))
            {
                _allEntities.Remove(entity);
                _entitiesById.Remove(id);
                foreach (var list in _entitiesByTag.Values) list.Remove(entity);
            }
        }

        // --- EXTREMELY FAST QUERIES FOR SHADERS / LOGIC ---
        public GameEntity GetById(string id) => _entitiesById.TryGetValue(id, out var e) ? e : null;
        public IReadOnlyList<GameEntity> GetByTag(string tag)
            => _entitiesByTag.TryGetValue(tag, out var list) ? list : new List<GameEntity>();
        public void Clear()
        {
            _allEntities.Clear();
            _entitiesById.Clear();
            _entitiesByTag.Clear();
        }
        private void SetupRenderState(GraphicsDevice gd, Camera camera)
        {
            gd.BlendState = BlendState.AlphaBlend;
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.SamplerStates[0] = SamplerState.PointClamp;

            _basicEffect.Projection = Matrix.CreateOrthographicOffCenter(0, camera.SimViewBounds.Width * camera.Zoom, camera.SimViewBounds.Height * camera.Zoom, 0, 0, 1);
            _basicEffect.View = camera.SimTransform;
            _basicEffect.World = Matrix.Identity;
        }
        // Helper to calculate the 4 corners of a sprite with Parallax
        private void CalculateSpriteGeometry(RenderableSprite sprite, Camera camera, bool enableParallax, float parallaxStrength, out Vector2 tl, out Vector2 tr, out Vector2 bl, out Vector2 br)
        {
            float left = -sprite.Origin.X * sprite.Scale.X;
            float right = (sprite.SourceRect.Width - sprite.Origin.X) * sprite.Scale.X;
            float top = -sprite.Origin.Y * sprite.Scale.Y;
            float bottom = (sprite.SourceRect.Height - sprite.Origin.Y) * sprite.Scale.Y;

            float cos = 1f, sin = 0f;
            if (sprite.Rotation != 0f)
            {
                cos = (float)Math.Cos(sprite.Rotation);
                sin = (float)Math.Sin(sprite.Rotation);
            }

            tl = new Vector2(sprite.Position.X + (left * cos - top * sin), sprite.Position.Y + (left * sin + top * cos));
            tr = new Vector2(sprite.Position.X + (right * cos - top * sin), sprite.Position.Y + (right * sin + top * cos));
            bl = new Vector2(sprite.Position.X + (left * cos - bottom * sin), sprite.Position.Y + (left * sin + bottom * cos));
            br = new Vector2(sprite.Position.X + (right * cos - bottom * sin), sprite.Position.Y + (right * sin + bottom * cos));

            if (enableParallax && sprite.ParallaxMask > 0)
            {
                Vector2 camOffset = sprite.Position - camera.Position;
                Vector2 shift = camOffset * parallaxStrength * sprite.ParallaxMask;
                tl += shift; tr += shift;
            }
        }
        public void DrawDebugOverlays(SpriteBatch sb, GameState state)
        {
            float lineThickness = 1f / state.GameCamera.Zoom;

            foreach (var entity in _allEntities)
            {
                if (entity.BaseData is PropObject && !(state.DebugPool[GameBool.ShowCollision] && entity.BaseData.Tags.Contains("#solid")))
                    continue;

                Color tagColor = Color.Magenta;
                if (entity.BaseData.Tags.Count > 0)
                {
                    var tagDef = state.tagManager.GetTag(entity.BaseData.Tags.First());
                    if (tagDef != null) tagColor = tagDef.TagColor;
                }

                if (state.DebugPool[GameBool.ShowShapes] || (state.DebugPool[GameBool.ShowCollision] && entity.BaseData.Tags.Contains("#solid")))
                {
                    if (entity.BaseData is RectangleObject r)
                    {
                        var bounds = new RectangleF(r.Position, r.Size);
                        sb.FillRectangle(bounds, tagColor * 0.3f);
                        sb.DrawRectangle(bounds, tagColor, lineThickness);
                    }
                    else if (entity.BaseData is ShapeObject s)
                    {
                        var verts = s.Shape.Vertices;
                        for (int i = 0; i < verts.Count; i++) sb.DrawLine(verts[i], verts[(i + 1) % verts.Count], tagColor, lineThickness * 2);
                    }
                    else if (entity.BaseData is PointObject p)
                    {
                        sb.DrawCircle(p.Position, p.Radius, 16, tagColor, lineThickness);
                    }
                    else if (entity.BaseData is PropObject prop && entity.Prefab != null)
                    {
                        RectangleF bounds = new RectangleF(prop.Position.X - entity.Prefab.Pivot.X, prop.Position.Y - entity.Prefab.Pivot.Y, entity.Prefab.SourceRect.Width, entity.Prefab.SourceRect.Height);
                        sb.DrawRectangle(bounds, tagColor, lineThickness);
                    }
                }

                if (state.DebugPool[GameBool.ShowLinks] && entity.BaseData.LinkedObjects.Count > 0)
                {
                    Vector2 start = entity.Position;
                    if (entity.BaseData is RectangleObject ro) start = new Vector2(ro.Position.X + ro.Size.X / 2, ro.Position.Y + ro.Size.Y / 2);
                    else if (entity.BaseData is ShapeObject so) start = new Vector2(so.Position.X + so.Size.X / 2, so.Position.Y + so.Size.Y / 2);

                    foreach (string targetId in entity.BaseData.LinkedObjects)
                    {
                        var targetEntity = GetById(targetId);
                        if (targetEntity != null)
                        {
                            Vector2 end = targetEntity.Position;
                            if (targetEntity.BaseData is RectangleObject rTarget) end = new Vector2(rTarget.Position.X + rTarget.Size.X / 2, rTarget.Position.Y + rTarget.Size.Y / 2);
                            else if (targetEntity.BaseData is ShapeObject sTarget) end = new Vector2(sTarget.Position.X + sTarget.Size.X / 2, sTarget.Position.Y + sTarget.Size.Y / 2);
                            Pixel_Simulations.UI.UIDrawExtensions.DrawDashedLine(sb, start, end, Color.Cyan, lineThickness * 2, 10f * lineThickness, 5f * lineThickness);
                        }
                    }
                }
            }
        }
        // ==========================================
        // 1. DEDICATED ALBEDO PASS (Standard Colors)
        // ==========================================
        public void DrawAlbedoPass(GraphicsDevice gd, Camera camera, bool enableParallax, float parallaxStrength)
        {
            if (_drawList.Count == 0) return;
            SetupRenderState(gd, camera);

            int spriteCount = 0;
            Texture2D currentTexture = null;

            foreach (var sprite in _drawList)
            {
                if (spriteCount >= MAX_SPRITES || (currentTexture != null && currentTexture != sprite.Texture))
                {
                    FlushBatch(gd, _basicEffect, currentTexture, spriteCount);
                    spriteCount = 0;
                }
                currentTexture = sprite.Texture;

                CalculateSpriteGeometry(sprite, camera, enableParallax, parallaxStrength, out Vector2 tl, out Vector2 tr, out Vector2 bl, out Vector2 br);

                float texW = 1f / currentTexture.Width; float texH = 1f / currentTexture.Height;
                float u0 = (sprite.SourceRect.X * texW) + (0.1f * texW); float v0 = (sprite.SourceRect.Y * texH) + (0.1f * texH);
                float u1 = ((sprite.SourceRect.Right) * texW) - (0.1f * texW); float v1 = ((sprite.SourceRect.Bottom) * texH) - (0.1f * texH);

                int vIdx = spriteCount * 4;
                _vertices[vIdx + 0] = new VertexPositionColorTexture(new Vector3(tl, 0), Color.White, new Vector2(u0, v0));
                _vertices[vIdx + 1] = new VertexPositionColorTexture(new Vector3(tr, 0), Color.White, new Vector2(u1, v0));
                _vertices[vIdx + 2] = new VertexPositionColorTexture(new Vector3(bl, 0), Color.White, new Vector2(u0, v1));
                _vertices[vIdx + 3] = new VertexPositionColorTexture(new Vector3(br, 0), Color.White, new Vector2(u1, v1));
                spriteCount++;
            }
            if (spriteCount > 0) FlushBatch(gd, _basicEffect, currentTexture, spriteCount);
        }

        // ==========================================
        // 2. DEDICATED NORMAL PASS
        // ==========================================
        public void DrawNormalPass(GraphicsDevice gd, Camera camera, AssetLibrary assets, bool enableParallax, float parallaxStrength)
        {
            if (_drawList.Count == 0) return;
            SetupRenderState(gd, camera);

            int spriteCount = 0;
            Texture2D currentTexture = null;

            foreach (var sprite in _drawList)
            {
                // LOOKUP THE NORMAL MAP! Fallback to original texture if missing.
                Texture2D targetTex = assets.GetNormalAtlas(sprite.AtlasName) ?? sprite.Texture;

                if (spriteCount >= MAX_SPRITES || (currentTexture != null && currentTexture != targetTex))
                {
                    FlushBatch(gd, _basicEffect, currentTexture, spriteCount);
                    spriteCount = 0;
                }
                currentTexture = targetTex;

                CalculateSpriteGeometry(sprite, camera, enableParallax, parallaxStrength, out Vector2 tl, out Vector2 tr, out Vector2 bl, out Vector2 br);

                float texW = 1f / currentTexture.Width; float texH = 1f / currentTexture.Height;
                float u0 = (sprite.SourceRect.X * texW) + (0.1f * texW); float v0 = (sprite.SourceRect.Y * texH) + (0.1f * texH);
                float u1 = ((sprite.SourceRect.Right) * texW) - (0.1f * texW); float v1 = ((sprite.SourceRect.Bottom) * texH) - (0.1f * texH);

                int vIdx = spriteCount * 4;
                _vertices[vIdx + 0] = new VertexPositionColorTexture(new Vector3(tl, 0), Color.White, new Vector2(u0, v0));
                _vertices[vIdx + 1] = new VertexPositionColorTexture(new Vector3(tr, 0), Color.White, new Vector2(u1, v0));
                _vertices[vIdx + 2] = new VertexPositionColorTexture(new Vector3(bl, 0), Color.White, new Vector2(u0, v1));
                _vertices[vIdx + 3] = new VertexPositionColorTexture(new Vector3(br, 0), Color.White, new Vector2(u1, v1));
                spriteCount++;
            }
            if (spriteCount > 0) FlushBatch(gd, _basicEffect, currentTexture, spriteCount);
        }

        // ==========================================
        // 3. DEDICATED VOLUME DEPTH PASS
        // ==========================================
        public void DrawDepthPass(GraphicsDevice gd, Camera camera, Effect depthEffect, bool enableParallax, float parallaxStrength)
        {
            if (_drawList.Count == 0 || depthEffect == null) return;

            SetupRenderState(gd, camera);

            // Setup Custom Shader
            Matrix projection = Matrix.CreateOrthographicOffCenter(0, camera.SimViewBounds.Width * camera.Zoom, camera.SimViewBounds.Height * camera.Zoom, 0, 0, 1);
            depthEffect.Parameters["MatrixTransform"]?.SetValue(camera.SimTransform * projection);

            int spriteCount = 0;
            Texture2D currentTexture = null;

            foreach (var sprite in _drawList)
            {
                if (spriteCount >= MAX_SPRITES || (currentTexture != null && currentTexture != sprite.Texture))
                {
                    FlushBatch(gd, depthEffect, currentTexture, spriteCount);
                    spriteCount = 0;
                }
                currentTexture = sprite.Texture;

                CalculateSpriteGeometry(sprite, camera, enableParallax, parallaxStrength, out Vector2 tl, out Vector2 tr, out Vector2 bl, out Vector2 br);

                float texW = 1f / currentTexture.Width; float texH = 1f / currentTexture.Height;
                float u0 = (sprite.SourceRect.X * texW) + (0.1f * texW); float v0 = (sprite.SourceRect.Y * texH) + (0.1f * texH);
                float u1 = ((sprite.SourceRect.Right) * texW) - (0.1f * texW); float v1 = ((sprite.SourceRect.Bottom) * texH) - (0.1f * texH);

                // PACK DEPTH VALUES INTO VERTEX COLOR! (Red = DrawDepth, Green = Physical World Y)
                Color depthColor = new Color(sprite.DrawDepth, DepthUtil.Calculate(sprite.BaseWorldY), 0, 1f);

                int vIdx = spriteCount * 4;
                _vertices[vIdx + 0] = new VertexPositionColorTexture(new Vector3(tl, 0), depthColor, new Vector2(u0, v0));
                _vertices[vIdx + 1] = new VertexPositionColorTexture(new Vector3(tr, 0), depthColor, new Vector2(u1, v0));
                _vertices[vIdx + 2] = new VertexPositionColorTexture(new Vector3(bl, 0), depthColor, new Vector2(u0, v1));
                _vertices[vIdx + 3] = new VertexPositionColorTexture(new Vector3(br, 0), depthColor, new Vector2(u1, v1));
                spriteCount++;
            }
            if (spriteCount > 0) FlushBatch(gd, depthEffect, currentTexture, spriteCount);
        }
        private void FlushBatch(GraphicsDevice gd, Effect effect, Texture2D texture, int spriteCount)
        {
            if (spriteCount == 0 || texture == null) return;

            if (effect is BasicEffect basic) basic.Texture = texture;
            else effect.Parameters["SpriteTexture"]?.SetValue(texture); // Your custom shader

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, spriteCount * 4, _indices, 0, spriteCount * 2);
            }
        }









    }
}