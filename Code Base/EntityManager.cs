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
        public HashSet<int> ActiveTags { get; set; } = new HashSet<int>();
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
        private readonly Dictionary<int, List<GameEntity>> _entitiesByTag = new Dictionary<int, List<GameEntity>>();

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
                {

                    foreach (var obj in objLayer.Objects)
                    {
                        // --- NEW: SKIP IF DESTROYED BY PLAYER ---
                        if (state.ActiveSave.DestroyedBaseIDs.Contains(obj.ID)) continue;

                        RegisterObject(obj, prefabManager);
                    }
                }

                if (layer is ControlLayer ctrlLayer)
                    {
                        foreach (var rect in ctrlLayer.Rectangles)
                        {
                            if (state.ActiveSave.DestroyedBaseIDs.Contains(rect.ID)) continue;
                            RegisterObject(rect, prefabManager);
                        }
                        foreach (var shape in ctrlLayer.Shapes)
                        {
                            if (state.ActiveSave.DestroyedBaseIDs.Contains(shape.ID)) continue;
                            RegisterObject(shape, prefabManager);
                        }
                        foreach (var pt in ctrlLayer.Rectangles)
                        {
                            if (state.ActiveSave.DestroyedBaseIDs.Contains(pt.ID)) continue;
                            RegisterObject(pt, prefabManager);
                        }
                    }
            }

            // 2. INJECT PLAYER PLACED ITEMS
            foreach (var placedItem in state.ActiveSave.PlacedItems)
            {
                RegisterObject(placedItem, prefabManager);
            }

            // 3. Cache Static Sprites
            RebuildStaticSprites(state);

        }
        public void RebuildStaticSprites(GameState state)
        {
            _staticSprites.Clear();
            foreach (var entity in _allEntities)
            {
                if (!entity.IsActive) continue;

                // Standard Props from the Map Editor
                if (entity.BaseData is PropObject prop && entity.Prefab != null)
                {
                    var tex = state.Assets.GetAtlas(entity.Prefab.AtlasName);
                    if (tex != null)
                    {
                        string stateName = entity.GetProperty("CurrentState", "");
                        Rectangle srcRect = entity.Prefab.SourceRect;
                        if (!string.IsNullOrEmpty(stateName) && entity.Prefab.AlternateStates.TryGetValue(stateName, out Rectangle stateRect))
                            srcRect = stateRect;

                        _staticSprites.Add(new RenderableSprite
                        {
                            AtlasName = entity.Prefab.AtlasName,
                            Texture = tex,
                            Position = entity.Position,
                            SourceRect = srcRect,
                            Origin = entity.Prefab.Pivot,
                            Scale = prop.Scale,
                            Rotation = prop.Rotation,
                            BaseWorldY = entity.Position.Y,
                            ParallaxMask = 0f
                        });
                    }
                }
                // NEW: Player-Placed Items from the Save File
                else if (entity.BaseData is PlacedItemObject placedItem)
                {
                    var physDef = state.ItemManager.GetPhysicalDef(placedItem.ItemID);
                    if (physDef != null && physDef.Stages.Count > 0)
                    {
                        var tex = state.Assets.GetAtlas(physDef.AtlasName);
                        if (tex != null)
                        {
                            int stageIdx = Math.Min(placedItem.CurrentStageIndex, physDef.Stages.Count - 1);
                            var stage = physDef.Stages[stageIdx];

                            Rectangle srcRect = new Rectangle(stage.SpriteX * 16, stage.SpriteY * 16, physDef.CellWidth * 16, physDef.CellHeight * 16);
                            Vector2 pivot = new Vector2(srcRect.Width / 2f, srcRect.Height); // Bottom Center

                            _staticSprites.Add(new RenderableSprite
                            {
                                AtlasName = physDef.AtlasName,
                                Texture = tex,
                                Position = entity.Position,
                                SourceRect = srcRect,
                                Origin = pivot,
                                Scale = Vector2.One,
                                Rotation = 0f,
                                BaseWorldY = entity.Position.Y,
                                ParallaxMask = 0f
                            });
                        }
                    }
                }
            }
            _isFirstLoad = true; // Forces the Y-Sorter to run next frame
        }
        public void AddDynamicEntity(MapObject obj, GameState state)
        {
            RegisterObject(obj, state.PrefabManager);
            RebuildStaticSprites(state);
        }
        private void RegisterObject(MapObject obj, PrefabManager prefabManager)
        {
            var entity = new GameEntity
            {
                BaseData = obj,
                Position = obj.Position,
                Prefab = (obj is PropObject prop) ? prefabManager.GetPrefab(prop.PrefabID) : null
            };
            entity.ActiveTags = obj.Tags;
            if (entity.Prefab != null) entity.ActiveTags.UnionWith(entity.Prefab.Tags);
            _allEntities.Add(entity);
            _entitiesById[obj.ID] = entity;

            // Merge Instance tags and Prefab tags
            foreach (var tag in entity.ActiveTags)
            {
                if (!_entitiesByTag.ContainsKey(tag)) _entitiesByTag[tag] = new List<GameEntity>();
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
                var P = state.Player;
                _drawList.Add(new RenderableSprite
                {
                    AtlasName = "Player",
                    Texture = P.Texture,
                    Position = P.Position, // Draw at the exact foot world position!
                    SourceRect = P.SourceRect,
                    Origin = P.Origin, // Pivot around the bottom-center of the sprite!
                    Scale = Vector2.One,
                    Rotation = 0f,
                    DrawDepth = DepthUtil.Calculate(P.Foot.Y), // Sort by foot
                    BaseWorldY = P.Foot.Y,
                    ParallaxMask = 0.0f
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
        public IReadOnlyList<GameEntity> GetByTag(int tag)
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
                if (entity.BaseData is PropObject && !(state.DebugPool[GameBool.ShowCollision] && entity.BaseData.Tags.Contains(3)))
                    continue;

                Color tagColor = Color.Magenta;
                if (entity.BaseData.Tags.Count > 0)
                {
                    var tagDef = state.tagManager.GetTag(entity.BaseData.Tags.First());
                    if (tagDef != null) tagColor = tagDef.TagColor;
                }

                if (state.DebugPool[GameBool.ShowShapes] || (state.DebugPool[GameBool.ShowCollision] && entity.BaseData.Tags.Contains(3)))
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

        // Inside EntityManager.cs
        public void RefreshEntityVisuals(string entityId)
        {
            var entity = GetById(entityId);
            if (entity == null || entity.Prefab == null) return;

            for (int i = 0; i < _staticSprites.Count; i++)
            {
                if (_staticSprites[i].Position == entity.Position)
                {
                    var sprite = _staticSprites[i];

                    // Check if the entity has a "CurrentState" property
                    string stateName = entity.GetProperty("CurrentState", "");

                    // If it does, and the Prefab has a matching rectangle, use it!
                    if (!string.IsNullOrEmpty(stateName) && entity.Prefab.AlternateStates.TryGetValue(stateName, out Rectangle stateRect))
                    {
                        sprite.SourceRect = stateRect;
                    }
                    else
                    {
                        sprite.SourceRect = entity.Prefab.SourceRect; // Default fallback
                    }

                    _staticSprites[i] = sprite;
                    _isFirstLoad = true; // Force re-sort
                    break;
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
            //SetupRenderState(gd, camera);
            gd.BlendState = new BlendState
            {
                ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Green,
                ColorSourceBlend = Blend.One,
                ColorDestinationBlend = Blend.Zero
            };
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.SamplerStates[0] = SamplerState.PointClamp;


            // Setup Custom Shader
            Matrix projection = Matrix.CreateOrthographicOffCenter(0, camera.SimViewBounds.Width * camera.Zoom, camera.SimViewBounds.Height * camera.Zoom, 0, 0, 1);
            depthEffect.Parameters["MatrixTransform"]?.SetValue(camera.ScreenWVP);

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

                float z = sprite.BaseWorldY; // <--- THIS IS THE FIX! Pass the actual ground Y into the Z coordinate

                int vIdx = spriteCount * 4;
                _vertices[vIdx + 0] = new VertexPositionColorTexture(new Vector3(tl, z), depthColor, new Vector2(u0, v0));
                _vertices[vIdx + 1] = new VertexPositionColorTexture(new Vector3(tr, z), depthColor, new Vector2(u1, v0));
                _vertices[vIdx + 2] = new VertexPositionColorTexture(new Vector3(bl, z), depthColor, new Vector2(u0, v1));
                _vertices[vIdx + 3] = new VertexPositionColorTexture(new Vector3(br, z), depthColor, new Vector2(u1, v1));
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