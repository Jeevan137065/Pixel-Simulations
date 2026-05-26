using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    public class GameEntity
    {
        public MapObject BaseData { get; set; }
        public ObjectPrefab Prefab { get; set; }
        public Vector2 Position { get; set; }
        public bool IsActive { get; set; } = true;
        public HashSet<int> ActiveTags { get; set; } = new HashSet<int>();
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
        public RectangleF Bounds { get; set; }
        public VertexPositionColor[] Vertices { get; set; }
        public int PrimitiveCount { get; set; }
        public PrimitiveType PrimitiveType { get; set; }
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

        public readonly List<RenderableSprite> _simulationSprites = new List<RenderableSprite>();
        public IReadOnlyList<GameEntity> AllEntities => _allEntities;
        public readonly List<RenderableSprite> _staticSprites = new List<RenderableSprite>();
        public readonly List<RenderableSprite> _drawList = new List<RenderableSprite>();
        private bool _isFirstLoad = true;

        private const int MAX_SPRITES = 4096;
        private VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[MAX_SPRITES * 4];
        private short[] _indices = new short[MAX_SPRITES * 6];
        private BasicEffect _basicEffect;

        // Texture Cache for Layer Compositing
        private Dictionary<string, Texture2D> _compositeCache = new Dictionary<string, Texture2D>();

        public void LoadFromMap(Map map, PrefabManager prefabManager, GameState state, GraphicsDevice gd)
        {
            Clear();
            int objCount = 0;

            for (int i = 0; i < MAX_SPRITES; i++)
            {
                _indices[i * 6 + 0] = (short)(i * 4 + 0);
                _indices[i * 6 + 1] = (short)(i * 4 + 1);
                _indices[i * 6 + 2] = (short)(i * 4 + 2);
                _indices[i * 6 + 3] = (short)(i * 4 + 1);
                _indices[i * 6 + 4] = (short)(i * 4 + 3);
                _indices[i * 6 + 5] = (short)(i * 4 + 2);
            }

            if (_basicEffect == null)
            {
                _basicEffect = new BasicEffect(gd) { TextureEnabled = true, VertexColorEnabled = true };
            }

            if (map == null)
            {
                System.Diagnostics.Debug.WriteLine("[EntityManager] WARNING: Map is null!");
                return;
            }

            foreach (var layer in map.Layers)
            {
                if (layer is ObjectLayer objLayer && layer.Type == LayerType.Object)
                {
                    foreach (var obj in objLayer.Objects)
                    {
                        if (state.ActiveSave.DestroyedBaseIDs.Contains(obj.ID)) continue;
                        RegisterObject(obj, prefabManager);
                        objCount++;
                    }
                }
                else if (layer is ControlLayer ctrlLayer)
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
                    foreach (var pt in ctrlLayer.Points)
                    {
                        if (state.ActiveSave.DestroyedBaseIDs.Contains(pt.ID)) continue;
                        RegisterObject(pt, prefabManager);
                    }
                }
            }

            foreach (var placedItem in state.ActiveSave.PlacedItems)
            {
                RegisterObject(placedItem, prefabManager);
                objCount++;
            }

            System.Diagnostics.Debug.WriteLine($"[EntityManager] Loaded {objCount} dynamic objects from Map & Save Data.");
            RebuildStaticSprites(state);
        }

        // --- LAYER RENDERING ORDER ---
        private int GetLayerZIndex(string name)
        {
            if (name == "Base") return 0;
            if (name.Contains("Base")) return 1;      // State_Base
            if (name.Contains("InteriorBG")) return 2;
            if (name.Contains("Trunk")) return 3;     // State_Trunk
            if (name.Contains("Exterior")) return 4;
            if (name.Contains("InteriorFG")) return 5;
            if (name.Contains("Roof")) return 6;
            if (name.StartsWith("Stage_")) return 7;
            if (name.StartsWith("Overlay_")) return 8;// Leaves/Snow draw ON TOP of trunk
            return 10;
        }

        private List<string> GetActiveLayersForEntity(GameEntity entity, GameState state)
        {
            List<string> activeLayers = new List<string>();
            string type = entity.GetProperty("Type", "Prop");
            bool isChopped = entity.GetProperty("Chopped", "False") == "True";

            if (type == "Tree")
            {
                if (isChopped)
                {
                    if (entity.Prefab.AlternateStates.ContainsKey("State_Base")) activeLayers.Add("State_Base");
                }
                else
                {
                    if (entity.Prefab.AlternateStates.ContainsKey("State_Base")) activeLayers.Add("State_Base");
                    if (entity.Prefab.AlternateStates.ContainsKey("State_Trunk")) activeLayers.Add("State_Trunk");

                    // Map engine seasons to exact HTML tool strings
                    string seasonName = state.Calendar.CurrentSeason.ToString();
                    if (state.Calendar.CurrentSeason == Season.Winter) seasonName = "Snow";
                    if (state.Calendar.CurrentSeason == Season.Fall) seasonName = "Autumn";

                    string overlayKey = "Overlay_" + seasonName;

                    if (entity.Prefab.AlternateStates.ContainsKey(overlayKey)) activeLayers.Add(overlayKey);
                    else if (entity.Prefab.AlternateStates.ContainsKey("Overlay_Summer")) activeLayers.Add("Overlay_Summer");
                    else if (entity.Prefab.AlternateStates.ContainsKey("Overlay_Spring")) activeLayers.Add("Overlay_Spring");
                }
            }
            else
            {
                activeLayers.Add("Base");
                foreach (var kvp in entity.Prefab.AlternateStates)
                {
                    if (entity.GetProperty(kvp.Key, "False") == "True") activeLayers.Add(kvp.Key);
                }
            }

            return activeLayers;
        }

        private Texture2D GetOrCreateComposite(ObjectPrefab prefab, List<string> activeStates, GameState state)
        {
            // FIX: Sort visually (Base -> Trunk -> Snow) instead of alphabetically!
            activeStates.Sort((a, b) => GetLayerZIndex(a).CompareTo(GetLayerZIndex(b)));

            string cacheKey = prefab.ID + "_" + string.Join("_", activeStates);
            if (_compositeCache.TryGetValue(cacheKey, out var existingTex)) return existingTex;

            var gd = state._gd;
            var oldTargets = gd.GetRenderTargets();

            int width = Math.Max(16, prefab.CanvasSize.X);
            int height = Math.Max(16, prefab.CanvasSize.Y);

            RenderTarget2D rt = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

            gd.SetRenderTarget(rt);
            gd.Clear(Color.Transparent);

            using (var sb = new SpriteBatch(gd))
            {
                sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);

                bool drawBase = prefab.SourceRect.Width > 0 && !string.IsNullOrEmpty(prefab.AtlasName);
                if (prefab.SourceRect.Width <= 16 && prefab.AlternateStates.Count > 0) drawBase = false;

                if (activeStates.Contains("Base") && drawBase)
                {
                    var baseTex = state.Assets.GetAtlas(prefab.AtlasName);
                    if (baseTex != null)
                        sb.Draw(baseTex, prefab.Offset.ToVector2(), prefab.SourceRect, Color.White);
                }

                foreach (var stateKey in activeStates)
                {
                    if (prefab.AlternateStates.TryGetValue(stateKey, out var layerData))
                    {
                        var layerTex = state.Assets.GetAtlas(layerData.AtlasName);
                        if (layerTex != null)
                            sb.Draw(layerTex, layerData.Offset.ToVector2(), layerData.SourceRect, Color.White);
                    }
                }
                sb.End();
            }

            gd.SetRenderTargets(oldTargets);
            _compositeCache[cacheKey] = rt;
            return rt;
        }

        public void RebuildStaticSprites(GameState state)
        {
            _staticSprites.Clear();
            _simulationSprites.Clear();
            int builtCount = 0;

            foreach (var entity in _allEntities)
            {
                if (!entity.IsActive) continue;

                if (entity.BaseData is PropObject prop)
                {
                    if (entity.Prefab == null) continue;

                    // Centralized Logic
                    List<string> activeLayers = GetActiveLayersForEntity(entity, state);
                    Texture2D finalTexture = GetOrCreateComposite(entity.Prefab, activeLayers, state);
                    float heightFactor = 0f;
                    string type = entity.GetProperty("Type", "Prop");
                    bool isChopped = entity.GetProperty("Chopped", "False") == "True";
                    if (type == "Tree" && !isChopped)
                    {
                        // Lerp parallax strength based on height. (64px = 0.0, 320px = 1.0)
                        heightFactor = MathHelper.Clamp((finalTexture.Height - 64f) / (320f - 64f), 0f, 1f);
                    }
                    _staticSprites.Add(new RenderableSprite
                    {
                        AtlasName = "COMPOSITE",
                        Texture = finalTexture,
                        Position = entity.Position,
                        SourceRect = new Rectangle(0, 0, finalTexture.Width, finalTexture.Height),
                        Origin = entity.Prefab.Pivot,
                        Scale = prop.Scale,
                        Rotation = prop.Rotation,
                        BaseWorldY = entity.Position.Y,
                        ParallaxMask = heightFactor
                    });
                    builtCount++;
                }
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
                            Vector2 pivot = new Vector2(srcRect.Width / 2f, srcRect.Height);

                            _simulationSprites.Add(new RenderableSprite
                            {
                                AtlasName = physDef.AtlasName,
                                Texture = tex,
                                Position = entity.Position,
                                SourceRect = srcRect,
                                Origin = pivot,
                                Scale = Vector2.One,
                                Rotation = 0f,
                                BaseWorldY = entity.Position.Y,
                                DrawDepth = DepthUtil.Calculate(entity.Position.Y),
                                ParallaxMask = 0f
                            });
                            builtCount++;
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[EntityManager] Successfully generated {builtCount} renderable sprites.");
            _isFirstLoad = true;
        }

        public void RefreshEntityVisuals(string entityId, GameState state)
        {
            var entity = GetById(entityId);
            if (entity == null || entity.Prefab == null) return;

            for (int i = 0; i < _staticSprites.Count; i++)
            {
                if (_staticSprites[i].Position == entity.Position)
                {
                    var sprite = _staticSprites[i];

                    // Centralized Logic
                    List<string> activeLayers = GetActiveLayersForEntity(entity, state);
                    Texture2D newTexture = GetOrCreateComposite(entity.Prefab, activeLayers, state);

                    sprite.Texture = newTexture;
                    sprite.SourceRect = new Rectangle(0, 0, newTexture.Width, newTexture.Height);
                    _staticSprites[i] = sprite;
                    break;
                }
            }
            _isFirstLoad = true;
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
            _drawList.AddRange(_simulationSprites);

            if (state.Player != null)
            {
                var P = state.Player;
                _drawList.Add(new RenderableSprite
                {
                    AtlasName = "Player",
                    Texture = P.Texture,
                    Position = P.Position,
                    SourceRect = P.SourceRect,
                    Origin = P.Origin,
                    Scale = Vector2.One,
                    Rotation = 0f,
                    DrawDepth = DepthUtil.Calculate(P.Foot.Y),
                    BaseWorldY = P.Foot.Y,
                    ParallaxMask = 0.0f
                });
            }

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

        public GameEntity GetById(string id) => _entitiesById.TryGetValue(id, out var e) ? e : null;
        public GameEntity GetEntity(string id) => _entitiesById.TryGetValue(id, out var e) ? e : null;
        public IReadOnlyList<GameEntity> GetByTag(int tag) => _entitiesByTag.TryGetValue(tag, out var list) ? list : new List<GameEntity>();

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
                Color tagColor = Color.Magenta;
                if (entity.BaseData.Tags.Count > 0)
                {
                    var tagDef = state.tagManager.GetTag(entity.BaseData.Tags.First());
                    if (tagDef != null) tagColor = tagDef.TagColor;
                }

                if (state.DebugPool[GameBool.ShowShapes] || state.DebugPool[GameBool.ShowCollision])
                {
                    if (entity.BaseData is RectangleObject r)
                    {
                        var bounds = new RectangleF(r.Position, r.Size);
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
                    else if (entity.BaseData is PropObject prop)
                    {
                        if (entity.Prefab != null)
                        {
                            int w = Math.Max(entity.Prefab.SourceRect.Width, entity.Prefab.CanvasSize.X);
                            int h = Math.Max(entity.Prefab.SourceRect.Height, entity.Prefab.CanvasSize.Y);
                            RectangleF bounds = new RectangleF(prop.Position.X - entity.Prefab.Pivot.X, prop.Position.Y - entity.Prefab.Pivot.Y, w * prop.Scale.X, h * prop.Scale.Y);
                            sb.DrawRectangle(bounds, tagColor, lineThickness);
                        }
                        else
                        {
                            // Draw an error box if the prefab is missing!
                            sb.DrawRectangle(new RectangleF(prop.Position.X - 8, prop.Position.Y - 8, 16, 16), Color.Red, lineThickness * 2);
                        }
                    }
                }
            }
        }

        // ==========================================
        // DEDICATED ALBEDO PASS
        // ==========================================
        public void DrawAlbedoPass(GraphicsDevice gd, Camera camera, bool enableParallax, float parallaxStrength, List<RenderableSprite> renderList)
        {
            if (renderList.Count == 0) return;
            SetupRenderState(gd, camera);

            int spriteCount = 0;
            Texture2D currentTexture = null;

            foreach (var sprite in renderList)
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
        // DEDICATED NORMAL PASS
        // ==========================================
        public void DrawNormalPass(GraphicsDevice gd, Camera camera, AssetLibrary assets, bool enableParallax, float parallaxStrength, List<RenderableSprite> renderList)
        {
            if (renderList.Count == 0) return;
            SetupRenderState(gd, camera);

            int spriteCount = 0;
            Texture2D currentTexture = null;

            foreach (var sprite in renderList)
            {
                Texture2D targetTex = assets.GetNormalAtlas(sprite.AtlasName);
                if (targetTex == null) continue;

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
        // DEDICATED VOLUME DEPTH PASS
        // ==========================================
        public void DrawDepthPass(GraphicsDevice gd, Camera camera, Effect depthEffect, bool enableParallax, float parallaxStrength, List<RenderableSprite> renderList)
        {
            if (renderList.Count == 0 || depthEffect == null) return;

            // Additive blending for Red (Altitude) and Green (Depth)
            gd.BlendState = new BlendState
            {
                ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Green,
                ColorSourceBlend = Blend.One,
                ColorDestinationBlend = Blend.Zero
            };
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.SamplerStates[0] = SamplerState.PointClamp;

            depthEffect.Parameters["MatrixTransform"]?.SetValue(camera.ScreenWVP);
            depthEffect.Parameters["MaxAltitude"]?.SetValue(350f); // Make sure this matches your environment

            int spriteCount = 0;
            Texture2D currentTexture = null;

            foreach (var sprite in renderList)
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

                // --- NEW: FIX DEPTH CALCULATION FOR SHADER ---
                // We must pass the exact Top and Bottom Y coordinates in world space.
                float spriteTopY = sprite.Position.Y - (sprite.Origin.Y * sprite.Scale.Y);
                float spriteBottomY = sprite.Position.Y + ((sprite.SourceRect.Height - sprite.Origin.Y) * sprite.Scale.Y);

                // Set parameters individually for this specific sprite. 
                // NOTE: Because we are batching, this requires the Shader to do the math per-vertex, NOT per-draw call.
                // We pack the top/bottom Y coordinates into the Z and W components of the vertex!

                float topZ = spriteTopY;
                float botZ = spriteBottomY;

                // Color R = Target Depth (For Z-Sorting)
                // Color G = Actual World Ground Y
                Color depthColor = new Color(sprite.DrawDepth, DepthUtil.Calculate(sprite.BaseWorldY), 0, 1f);

                int vIdx = spriteCount * 4;
                // We use the Z coordinate of the Vector3 to pass the World Y position of the vertex to the shader
                _vertices[vIdx + 0] = new VertexPositionColorTexture(new Vector3(tl, topZ), depthColor, new Vector2(u0, v0));
                _vertices[vIdx + 1] = new VertexPositionColorTexture(new Vector3(tr, topZ), depthColor, new Vector2(u1, v0));
                _vertices[vIdx + 2] = new VertexPositionColorTexture(new Vector3(bl, botZ), depthColor, new Vector2(u0, v1));
                _vertices[vIdx + 3] = new VertexPositionColorTexture(new Vector3(br, botZ), depthColor, new Vector2(u1, v1));

                spriteCount++;
            }
            if (spriteCount > 0) FlushBatch(gd, depthEffect, currentTexture, spriteCount);
        }

        private void FlushBatch(GraphicsDevice gd, Effect effect, Texture2D texture, int spriteCount)
        {
            if (spriteCount == 0 || texture == null) return;

            if (effect is BasicEffect basic) basic.Texture = texture;
            else effect.Parameters["SpriteTexture"]?.SetValue(texture);

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, spriteCount * 4, _indices, 0, spriteCount * 2);
            }
        }
    }
}