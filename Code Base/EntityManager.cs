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
        public Dictionary<string, Texture2D> _compositeCache = new Dictionary<string, Texture2D>();

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

            if (_basicEffect == null) _basicEffect = new BasicEffect(gd) { TextureEnabled = true, VertexColorEnabled = true };

            if (map == null) return;

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
                    foreach (var rect in ctrlLayer.Rectangles) { if (!state.ActiveSave.DestroyedBaseIDs.Contains(rect.ID)) RegisterObject(rect, prefabManager); }
                    foreach (var shape in ctrlLayer.Shapes) { if (!state.ActiveSave.DestroyedBaseIDs.Contains(shape.ID)) RegisterObject(shape, prefabManager); }
                    foreach (var pt in ctrlLayer.Points) { if (!state.ActiveSave.DestroyedBaseIDs.Contains(pt.ID)) RegisterObject(pt, prefabManager); }
                }
            }

            foreach (var placedItem in state.ActiveSave.PlacedItems)
            {
                RegisterObject(placedItem, prefabManager);
                objCount++;
            }

            System.Diagnostics.Debug.WriteLine($"[EntityManager] Loaded {objCount} dynamic objects.");
            RebuildStaticSprites(state);
        }

        // --- LAYER LOGIC ---
        private List<string> GetActiveLayersForEntity(GameEntity entity, GameState state)
        {
            List<string> activeLayers = new List<string>();
            string type = entity.GetProperty("Type", "Prop");
            bool isChopped = entity.GetProperty("Chopped", "False") == "True";

            if (type == "Tree")
            {
                // Always draw base
                if (entity.Prefab.AlternateStates.ContainsKey("Base")) activeLayers.Add("Base");

                if (!isChopped)
                {
                    if (entity.Prefab.AlternateStates.ContainsKey("Trunk")) activeLayers.Add("Trunk");

                    // Add Canopy based on Season
                    string seasonCanopy = "Canopy_" + state.Calendar.CurrentSeason.ToString();
                    if (entity.Prefab.AlternateStates.ContainsKey(seasonCanopy)) activeLayers.Add(seasonCanopy);
                    else if (entity.Prefab.AlternateStates.ContainsKey("Canopy_Summer")) activeLayers.Add("Canopy_Summer"); // Fallback

                    // Add Overlays (Snow, Moss, Fruits) based on conditions
                    if (state.Calendar.CurrentSeason == Season.Winter && entity.Prefab.AlternateStates.ContainsKey("Overlay_Snow")) activeLayers.Add("Overlay_Snow");
                    if (entity.GetProperty("HasFruit", "False") == "True" && entity.Prefab.AlternateStates.ContainsKey("Overlay_Fruits")) activeLayers.Add("Overlay_Fruits");
                    if (entity.GetProperty("HasMoss", "False") == "True" && entity.Prefab.AlternateStates.ContainsKey("Overlay_Moss")) activeLayers.Add("Overlay_Moss");
                }
            }
            else if (type == "Building")
            {
                if (entity.Prefab.AlternateStates.ContainsKey("Exterior")) activeLayers.Add("Exterior");
                if (entity.Prefab.AlternateStates.ContainsKey("Roof") && entity.GetProperty("PlayerInside", "False") == "False") activeLayers.Add("Roof");

                // Add Overlays like Snow to roofs
                if (state.Calendar.CurrentSeason == Season.Winter && entity.Prefab.AlternateStates.ContainsKey("Overlay_Snow")) activeLayers.Add("Overlay_Snow");
            }
            else // Standard Props
            {
                foreach (var kvp in entity.Prefab.AlternateStates)
                {
                    // By default props just draw everything unless gated by a property
                    if (entity.GetProperty(kvp.Key, "True") == "True") activeLayers.Add(kvp.Key);
                }
            }

            // Sort by ZIndex
            activeLayers.Sort((a, b) => entity.Prefab.AlternateStates[a].ZIndex.CompareTo(entity.Prefab.AlternateStates[b].ZIndex));
            return activeLayers;
        }

        // --- COMPOSITOR & PALETTE SWAPPER ---
        private Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.Transparent;
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length != 6) return Color.Transparent;

            // Try/Catch just in case the HTML tool generated a weird string
            try
            {
                return new Color(Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
            }
            catch { return Color.Transparent; }
        }
        private Color ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.White;
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length != 6) return Color.White;

            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color((int)r, (int)g, (int)b, 255);
        }
        private Texture2D ApplyPaletteSwap(Texture2D source, Rectangle srcRect, Dictionary<string, string> paletteMap, GraphicsDevice gd)
        {
            if (paletteMap == null || paletteMap.Count == 0) return source;

            Color[] data = new Color[srcRect.Width * srcRect.Height];
            // Fix: Pull from the specific source rectangle, not the whole atlas
            source.GetData(0, srcRect, data, 0, data.Length);

            var fastMap = new Dictionary<Color, Color>();
            foreach (var kvp in paletteMap)
            {
                Color original = ParseHex(kvp.Key);
                Color replacement = ParseHex(kvp.Value);
                //System.Diagnostics.Debug.WriteLine($"[Compositor] Read colors as {kvp.Key} -> {kvp.Value}");

                // Strip Alpha to guarantee strict RGB matching
                fastMap[new Color((int)original.R, (int)original.G, (int)original.B, (int)255)] = new Color((int)replacement.R, (int)replacement.G, (int)replacement.B, (int)255);
            }

            int swappedPixels = 0;
            int skippedpixels = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].A < 10)
                {
                    skippedpixels++;
                    continue; 
                } // Skip fully transparent pixels

                // Un-premultiply the alpha so we can compare the raw RGB values accurately!
                float alphaFactor = 1f;//255f / data[i].A;
                byte r = (byte)MathHelper.Clamp(data[i].R * alphaFactor, 0, 255);
                byte g = (byte)MathHelper.Clamp(data[i].G * alphaFactor, 0, 255);
                byte b = (byte)MathHelper.Clamp(data[i].B * alphaFactor, 0, 255);

                Color rawRGB = new Color((int)r, (int)g, (int)b, (int)255);

                if (fastMap.TryGetValue(rawRGB, out Color newColor))
                {
                    // Re-apply the original alpha to the new color
                    data[i] = new Color(newColor.R, newColor.G, newColor.B, data[i].A);
                    swappedPixels++;
                }
                else
                {
                    // FALLBACK: Find the closest color (Fixes MonoGame compression artifacts)
                    Color bestMatch = rawRGB;
                    int minDiff = int.MaxValue;

                    foreach (var mapKvp in fastMap)
                    {
                        int dr = rawRGB.R - mapKvp.Key.R;
                        int dg = rawRGB.G - mapKvp.Key.G;
                        int db = rawRGB.B - mapKvp.Key.B;
                        int diff = (dr * dr) + (dg * dg) + (db * db);

                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            bestMatch = mapKvp.Value;
                        }
                    }

                    // If it's a reasonably close match (Allows for slight compression variation)
                    if (minDiff < 500)
                    {
                        Color OldData = data[i];
                        data[i] = new Color(bestMatch.R, bestMatch.G, bestMatch.B, data[i].A);
                        //System.Diagnostics.Debug.WriteLine($"[Compositor] Swapping colors from {OldData} to{data[i]}");
                        swappedPixels++;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[Compositor] Successfully palette swapped {swappedPixels} pixels. Skipped Pixels {skippedpixels}");

            Texture2D newTex = new Texture2D(gd, srcRect.Width, srcRect.Height);
            newTex.SetData(data);
            return newTex;
        }

        private Texture2D GetOrCreateComposite(ObjectPrefab prefab, List<string> activeStates, GameState state)
        {
            string cacheKey = prefab.ID + "_" + string.Join("_", activeStates);
            if (_compositeCache.TryGetValue(cacheKey, out var existingTex)) return existingTex;

            var gd = state._gd;
            var oldTargets = gd.GetRenderTargets();

            int width = Math.Max(16, prefab.CanvasSize.X);
            int height = Math.Max(16, prefab.CanvasSize.Y);

            RenderTarget2D rt = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

            gd.SetRenderTarget(rt);
            gd.Clear(Color.Transparent);

            // Track temporary CPU-swapped textures to safely dispose of them after rendering completes
            List<Texture2D> tempTextures = new List<Texture2D>();

            using (var sb = new SpriteBatch(gd))
            {
                sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);

                foreach (var stateKey in activeStates)
                {
                    if (prefab.AlternateStates.TryGetValue(stateKey, out var layerData))
                    {
                        var layerTex = state.Assets.GetAtlas(layerData.AtlasName);
                        if (layerTex != null)
                        {
                            if (layerData.PaletteMap != null && layerData.PaletteMap.Count > 0)
                            {
                                // Apply Palette Swap on CPU and draw the temporary texture
                                Texture2D swappedTex = ApplyPaletteSwap(layerTex, layerData.SourceRect, layerData.PaletteMap, gd);
                                sb.Draw(swappedTex, layerData.Offset.ToVector2(), Color.White);

                                // Queue the temporary texture for safe disposal
                                tempTextures.Add(swappedTex);
                            }
                            else
                            {
                                sb.Draw(layerTex, layerData.Offset.ToVector2(), layerData.SourceRect, Color.White);
                            }
                        }
                    }
                }
                sb.End(); // Flushes all draw calls to the active RenderTarget (rt) on the GPU
            }

            // Now that the GPU has finished utilizing the textures, safely release their memory
            foreach (var tempTex in tempTextures)
            {
                tempTex.Dispose();
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

                if (entity.BaseData is PropObject prop && entity.Prefab != null)
                {
                    List<string> activeLayers = GetActiveLayersForEntity(entity, state);
                    Texture2D finalTexture = GetOrCreateComposite(entity.Prefab, activeLayers, state);

                    string type = entity.GetProperty("Type", "Prop");
                    bool isChopped = entity.GetProperty("Chopped", "False") == "True";

                    bool hasSway = activeLayers.Any(l => entity.Prefab.AlternateStates[l].Sway);

                    // --- NEW: PROPORTIONAL PARALLAX ---
                    // Trees under 64px don't parallax. Trees at 320px parallax fully (1.0).
                    float pMask = 0f;
                    if (type == "Tree" && !isChopped)
                    {
                        float rawHeight = entity.Prefab.CanvasSize.Y;
                        pMask = MathHelper.Clamp((rawHeight - 64f) / (320f - 64f), 0f, 1f);
                    }
                    if (hasSway) pMask += 0.5f; // Add extra sway if canopy is active
                    float feetY = entity.Position.Y + entity.Prefab.Pivot.Y;
                    _staticSprites.Add(new RenderableSprite
                    {
                        AtlasName = "COMPOSITE",
                        Texture = finalTexture,
                        Position = entity.Position,
                        SourceRect = new Rectangle(0, 0, finalTexture.Width, finalTexture.Height),
                        Origin = entity.Prefab.Pivot,
                        Scale = prop.Scale,
                        Rotation = prop.Rotation,
                        BaseWorldY = feetY, // Sort by the actual physical feet!
                        DrawDepth = DepthUtil.Calculate(feetY),
                        ParallaxMask = Math.Min(pMask, 1.0f) // Cap at 1.0 max
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
                    List<string> activeLayers = GetActiveLayersForEntity(entity, state);
                    sprite.Texture = GetOrCreateComposite(entity.Prefab, activeLayers, state);
                    sprite.SourceRect = new Rectangle(0, 0, sprite.Texture.Width, sprite.Texture.Height);

                    bool hasSway = activeLayers.Any(l => entity.Prefab.AlternateStates[l].Sway);
                    sprite.ParallaxMask = hasSway ? 1.0f : 0f;

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

                // 1. Get the current terrain height beneath the player's feet
                float groundHeight = state.WaterSystem.GetTerrainHeightAt(P.Foot);

                // 2. Baseline shoreline elevation (e.g. 180 representing lake edge)
                float shorelineWaterLevel = 100f / 255f;

                // 3. Submersion depth represents the difference
                float submergeAmount = Math.Max(0.0f, shorelineWaterLevel - groundHeight);

                int sinkOffset = 0;
                string playerMovementState = "WALKING";

                if (submergeAmount > 0.0f)
                {
                    // Check if player has entered the Deep Swimming Zone (Elevation <= 40 / 255)
                    if (groundHeight <= (40f / 255f))
                    {
                        playerMovementState = "SWIMMING";
                        sinkOffset = 26; // Submerge up to chest
                    }
                    else
                    {
                        playerMovementState = "SINKING/WADING";
                        sinkOffset = (int)(submergeAmount * 16f); // Gradual shins-to-waist crop (Max 16px)
                    }
                }

                // Output status to the dynamic debug log on screen
                state.Debug_LastCommand = $"PLAYER STATE: {playerMovementState} Surface {groundHeight}| Ground Alt: {Math.Round(groundHeight * 255)} | Depth: {Math.Round(submergeAmount * 255)}";

                Rectangle croppedSource = new Rectangle(
                    P.SourceRect.X,
                    P.SourceRect.Y,
                    P.SourceRect.Width,
                    P.SourceRect.Height - sinkOffset
                );

                Vector2 adjustedOrigin = new Vector2(P.Origin.X, P.Origin.Y - sinkOffset);

                _drawList.Add(new RenderableSprite
                {
                    AtlasName = "Player",
                    Texture = P.Texture,
                    Position = P.Position,
                    SourceRect = croppedSource,
                    Origin = adjustedOrigin,
                    Scale = Vector2.One,
                    Rotation = 0f,
                    ParallaxMask = 0.0f
                });
            }

            // Apply standardized math to calculate the actual physical ground contact point (feet) for sorting
            for (int i = 0; i < _drawList.Count; i++)
            {
                var sprite = _drawList[i];

                // Ground contact is the bottom edge of the sprite bounds in world-space
                float physicalFeetY = sprite.Position.Y + (sprite.SourceRect.Height - sprite.Origin.Y) * sprite.Scale.Y;

                sprite.BaseWorldY = physicalFeetY;
                sprite.DrawDepth = DepthUtil.Calculate(physicalFeetY);

                _drawList[i] = sprite;
            }

            // Sort the list based on uniform feet calculations
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
        public void DrawYDepthPass(GraphicsDevice gd, Camera camera, Effect depthEffect, bool enableParallax, float parallaxStrength, List<RenderableSprite> renderList)
        {
            if (renderList.Count == 0 || depthEffect == null) return;

            // Write only to Red (Volume Height) and Green (Shadow Occlusion)
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

                float texW = 1f / currentTexture.Width;
                float texH = 1f / currentTexture.Height;
                float u0 = (sprite.SourceRect.X * texW) + (0.1f * texW);
                float v0 = (sprite.SourceRect.Y * texH) + (0.1f * texH);
                float u1 = ((sprite.SourceRect.Right) * texW) - (0.1f * texW);
                float v1 = ((sprite.SourceRect.Bottom) * texH) - (0.1f * texH);

                // Height attributes
                float objHeight = sprite.SourceRect.Height * sprite.Scale.Y;

                // G-Channel: Ambient/Canopy shadow value (255 for thick foliage, 0 for standard entities)
                byte canopyShadow = (byte)(sprite.AtlasName.Contains("Trees") || sprite.AtlasName.Contains("Building") ? 255 : 0);

                // Interpolate the height gradient via the vertex colors (R: Height, G: Occlusion, B: Surface)
                Color topColor = new Color((byte)255, canopyShadow, (byte)0, (byte)255); // Top vertices (Full height)
                Color botColor = new Color((byte)0, canopyShadow, (byte)0, (byte)255);   // Bottom vertices (Base ground)

                int vIdx = spriteCount * 4;
                _vertices[vIdx + 0] = new VertexPositionColorTexture(new Vector3(tl, objHeight), topColor, new Vector2(u0, v0));
                _vertices[vIdx + 1] = new VertexPositionColorTexture(new Vector3(tr, objHeight), topColor, new Vector2(u1, v0));
                _vertices[vIdx + 2] = new VertexPositionColorTexture(new Vector3(bl, 0f), botColor, new Vector2(u0, v1));
                _vertices[vIdx + 3] = new VertexPositionColorTexture(new Vector3(br, 0f), botColor, new Vector2(u1, v1));
                spriteCount++;
            }
            if (spriteCount > 0) FlushBatch(gd, depthEffect, currentTexture, spriteCount);
        }
        public void DrawZDepthPass(GraphicsDevice gd, Camera camera, Effect depthEffect, bool enableParallax, float parallaxStrength, List<RenderableSprite> renderList)
        {
            if (renderList.Count == 0 || depthEffect == null) return;

            // Write only to Red (Sorting Depth) and Green (Shadow depth projection)
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

                float texW = 1f / currentTexture.Width;
                float texH = 1f / currentTexture.Height;
                float u0 = (sprite.SourceRect.X * texW) + (0.1f * texW);
                float v0 = (sprite.SourceRect.Y * texH) + (0.1f * texH);
                float u1 = ((sprite.SourceRect.Right) * texW) - (0.1f * texW);
                float v1 = ((sprite.SourceRect.Bottom) * texH) - (0.1f * texH);

                // Convert flat calculated feet depth into color value across the entire quad face
                byte sortingDepth = (byte)(sprite.DrawDepth * 255f);
                Color flatDepthColor = new Color(sortingDepth, (byte)0, (byte)0, (byte)255);

                int vIdx = spriteCount * 4;
                _vertices[vIdx + 0] = new VertexPositionColorTexture(new Vector3(tl, 0), flatDepthColor, new Vector2(u0, v0));
                _vertices[vIdx + 1] = new VertexPositionColorTexture(new Vector3(tr, 0), flatDepthColor, new Vector2(u1, v0));
                _vertices[vIdx + 2] = new VertexPositionColorTexture(new Vector3(bl, 0), flatDepthColor, new Vector2(u0, v1));
                _vertices[vIdx + 3] = new VertexPositionColorTexture(new Vector3(br, 0), flatDepthColor, new Vector2(u1, v1));
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