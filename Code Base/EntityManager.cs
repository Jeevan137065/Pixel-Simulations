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
        public int builtCount = 0;
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
                if (entity.Prefab.AlternateStates.ContainsKey("Base")) activeLayers.Add("Base");

                if (!isChopped)
                {
                    if (entity.Prefab.AlternateStates.ContainsKey("Trunk")) activeLayers.Add("Trunk");

                    // 1. Seasonal Canopy with Single "Canopy" Reskinnable Fallback
                    string seasonCanopy = "Canopy_" + state.Calendar.CurrentSeason.ToString();
                    if (entity.Prefab.AlternateStates.ContainsKey(seasonCanopy))
                    {
                        activeLayers.Add(seasonCanopy);
                    }
                    else if (entity.Prefab.AlternateStates.ContainsKey("Canopy"))
                    {
                        activeLayers.Add("Canopy"); // Generic reskinnable canopy
                    }
                    else if (entity.Prefab.AlternateStates.ContainsKey("Canopy_Summer"))
                    {
                        activeLayers.Add("Canopy_Summer");
                    }

                    // 2. Overlays
                    if (state.Calendar.CurrentSeason == Season.Winter && entity.Prefab.AlternateStates.ContainsKey("Overlay_Snow"))
                        activeLayers.Add("Overlay_Snow");

                    if (entity.Prefab.AlternateStates.ContainsKey("Overlay_Moss") && entity.GetProperty("HasMoss", "True") == "True")
                        activeLayers.Add("Overlay_Moss");

                    if (entity.Prefab.AlternateStates.ContainsKey("Overlay_Fruits") && entity.GetProperty("HasFruit", "False") == "True")
                        activeLayers.Add("Overlay_Fruits");

                    if (entity.Prefab.AlternateStates.ContainsKey("Overlay_Flowers") && entity.GetProperty("HasFlowers", "False") == "True")
                        activeLayers.Add("Overlay_Flowers");
                }
            }
            else if (type == "Building")
            {
                bool playerInside = entity.GetProperty("PlayerInside", "False") == "True";

                if (!playerInside)
                {
                    if (entity.Prefab.AlternateStates.ContainsKey("Exterior")) activeLayers.Add("Exterior");
                    if (entity.Prefab.AlternateStates.ContainsKey("Roof")) activeLayers.Add("Roof");
                    if (state.Calendar.CurrentSeason == Season.Winter && entity.Prefab.AlternateStates.ContainsKey("Overlay_Snow")) activeLayers.Add("Overlay_Snow");
                }
                else
                {
                    if (entity.Prefab.AlternateStates.ContainsKey("InteriorBG")) activeLayers.Add("InteriorBG");
                    if (entity.Prefab.AlternateStates.ContainsKey("InteriorFG")) activeLayers.Add("InteriorFG");
                }
            }
            else // Standard Props
            {
                foreach (var kvp in entity.Prefab.AlternateStates)
                {
                    if (entity.GetProperty(kvp.Key, "True") == "True") activeLayers.Add(kvp.Key);
                }
            }

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

            List<Texture2D> tempTextures = new List<Texture2D>();

            using (var sb = new SpriteBatch(gd))
            {
                sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);

                foreach (var stateKey in activeStates)
                {
                    if (prefab.AlternateStates.TryGetValue(stateKey, out var layerData))
                    {
                        if (!layerData.Visible) continue; // Respect layer visibility

                        if (string.Equals(layerData.AtlasName, "Base", StringComparison.OrdinalIgnoreCase) &&
                            layerData.SourceRect == new Rectangle(0, 0, 16, 16) &&
                            layerData.Offset == Point.Zero)
                        {
                            continue;
                        }

                        var layerTex = state.Assets.GetAtlas(layerData.AtlasName);
                        if (layerTex != null)
                        {
                            // Apply per-layer local pivot displacement
                            Vector2 drawPos = layerData.Offset.ToVector2() - layerData.Pivot.ToVector2();

                            // Resolve active variant palette map
                            Dictionary<string, string> activePalette = layerData.PaletteMap;
                            if (layerData.Variants != null && !string.IsNullOrEmpty(layerData.ActiveVariant) &&
                                layerData.Variants.TryGetValue(layerData.ActiveVariant, out var vData) && vData.PaletteMap != null)
                            {
                                activePalette = vData.PaletteMap;
                            }

                            if (activePalette != null && activePalette.Count > 0)
                            {
                                Texture2D swappedTex = ApplyPaletteSwap(layerTex, layerData.SourceRect, activePalette, gd);
                                sb.Draw(swappedTex, drawPos, Color.White);
                                tempTextures.Add(swappedTex);
                            }
                            else
                            {
                                sb.Draw(layerTex, drawPos, layerData.SourceRect, Color.White);
                            }
                        }
                    }
                }
                sb.End();
            }

            foreach (var tempTex in tempTextures) tempTex.Dispose();

            gd.SetRenderTargets(oldTargets);
            _compositeCache[cacheKey] = rt;
            return rt;
        }
        public void RebuildStaticSprites(GameState state)
        {
            _staticSprites.Clear();
            _simulationSprites.Clear();

            foreach (var entity in _allEntities)
            {
                if (!entity.IsActive) continue;

                if (entity.BaseData is PropObject prop && entity.Prefab != null)
                {
                    List<string> activeLayers = GetActiveLayersForEntity(entity, state);
                    Texture2D finalTexture = GetOrCreateComposite(entity.Prefab, activeLayers, state);

                    string type = entity.GetProperty("Type", "Prop");
                    bool isChopped = entity.GetProperty("Chopped", "False") == "True";

                    bool hasSway = activeLayers.Any(l => entity.Prefab.AlternateStates.ContainsKey(l) && entity.Prefab.AlternateStates[l].Sway);

                    float pMask = 0f;
                    if (type == "Tree" && !isChopped)
                    {
                        float rawHeight = entity.Prefab.CanvasSize.Y;
                        pMask = MathHelper.Clamp((rawHeight - 64f) / (320f - 64f), 0f, 1f);
                    }
                    if (hasSway) pMask += 0.5f;

                    string atlasName = "Building";
                    if (type == "Tree") atlasName = "Trees";
                    else if (entity.Prefab.AlternateStates.Count > 0)
                    {
                        var firstLayer = entity.Prefab.AlternateStates.Values.FirstOrDefault();
                        if (firstLayer != null && !string.IsNullOrEmpty(firstLayer.AtlasName))
                            atlasName = System.IO.Path.GetFileNameWithoutExtension(firstLayer.AtlasName);
                    }

                    float feetY = entity.Position.Y + (finalTexture.Height - entity.Prefab.Pivot.Y) * prop.Scale.Y;

                    _staticSprites.Add(new RenderableSprite
                    {
                        AtlasName = atlasName,
                        Texture = finalTexture,
                        Position = entity.Position,
                        SourceRect = new Rectangle(0, 0, finalTexture.Width, finalTexture.Height),
                        Origin = entity.Prefab.Pivot,
                        Scale = prop.Scale,
                        Rotation = prop.Rotation,
                        BaseWorldY = feetY,
                        DrawDepth = DepthUtil.Calculate(feetY),
                        ParallaxMask = Math.Min(pMask, 1.0f),

                        // Multi-strip physics configuration
                        HasWindSway = hasSway,
                        StripHeight = entity.Prefab.GetStripHeight(),
                        Stiffness = entity.Prefab.GetStiffness(),
                        WaveLength = entity.Prefab.GetWaveLength(),
                        FlutterStr = entity.Prefab.GetFlutterStr()
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
                                ParallaxMask = 0f,
                                HasWindSway = false
                            });
                            builtCount++;
                        }
                    }
                }
            }
            // 2. GENERATE SPRITES FOR ACTIVE WORLD PLANTS & WILD FORAGES
            foreach (var plant in state.ActiveSave.ActivePlants)
            {
                var def = state.plantManager.GetPlant(plant.PlantID);
                if (def == null || plant.CurrentStageIndex >= def.Stages.Count) continue;

                var stage = def.Stages[plant.CurrentStageIndex];
                var tex = state.Assets.GetAtlas(def.AtlasName);

                if (tex == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[EntityManager WARNING] Missing Atlas '{def.AtlasName}' for plant '{def.Name}'.");
                    continue;
                }

                int w = def.CellWidth * 16;
                int h = def.CellHeight * 16;
                Rectangle srcRect = new Rectangle(stage.SpriteX * 16, stage.SpriteY * 16, w, h);
                Vector2 pivot = new Vector2(w / 2f, h);

                _staticSprites.Add(new RenderableSprite
                {
                    AtlasName = def.AtlasName,
                    Texture = tex,
                    Position = plant.Position,
                    SourceRect = srcRect,
                    Origin = pivot,
                    Scale = Vector2.One,
                    Rotation = 0f,
                    BaseWorldY = plant.Position.Y,
                    DrawDepth = DepthUtil.Calculate(plant.Position.Y),
                    ParallaxMask = 0f
                });
                builtCount++;
            }

            System.Diagnostics.Debug.WriteLine($"[EntityManager] Total renderable static sprites built: {_staticSprites.Count}");
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
            // Real-time dynamic interior/cutaway trigger detection
            if (state.Player != null)
            {
                Vector2 playerFoot = state.Player.Foot;

                foreach (var entity in _allEntities)
                {
                    if (!entity.IsActive || entity.Prefab == null) continue;

                    string type = entity.GetProperty("Type", "Prop");
                    if (type == "Building")
                    {
                        float left = entity.Position.X - entity.Prefab.Pivot.X;
                        float top = entity.Position.Y - entity.Prefab.Pivot.Y;
                        float width = entity.Prefab.CanvasSize.X;
                        float height = entity.Prefab.CanvasSize.Y;

                        // Threshold area inside building (above the entrance threshold)
                        RectangleF interiorZone = new RectangleF(left + 8f, top + 16f, Math.Max(1f, width - 16f), Math.Max(1f, height - 24f));

                        bool isPlayerInside = interiorZone.Contains(playerFoot);
                        bool wasInside = entity.GetProperty("PlayerInside", "False") == "True";

                        if (isPlayerInside != wasInside)
                        {
                            entity.BaseData.Properties["PlayerInside"] = new MapProperty(PropertyType.Boolean, isPlayerInside.ToString());
                            RefreshEntityVisuals(entity.BaseData.ID, state);
                        }
                    }
                }
            }

            _drawList.Clear();
            _drawList.AddRange(_staticSprites);
            _drawList.AddRange(_simulationSprites);

            if (state.Player != null)
            {
                var P = state.Player;
                float groundHeight = state.WaterSystem.GetTerrainHeightAt(P.Foot);
                float shorelineWaterLevel = 100f / 255f;
                float submergeAmount = Math.Max(0.0f, shorelineWaterLevel - groundHeight);

                int sinkOffset = 0;
                if (submergeAmount > 0.0f)
                {
                    if (groundHeight <= (40f / 255f)) sinkOffset = 26;
                    else sinkOffset = (int)(submergeAmount * 16f);
                }

                Rectangle croppedSource = new Rectangle(P.SourceRect.X, P.SourceRect.Y, P.SourceRect.Width, P.SourceRect.Height - sinkOffset);
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

            for (int i = 0; i < _drawList.Count; i++)
            {
                var sprite = _drawList[i];
                float physicalFeetY = sprite.Position.Y + (sprite.SourceRect.Height - sprite.Origin.Y) * sprite.Scale.Y;
                sprite.BaseWorldY = physicalFeetY;
                sprite.DrawDepth = DepthUtil.Calculate(physicalFeetY);
                _drawList[i] = sprite;
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

        // NEW: Type-safe CodeName tag query
        public IReadOnlyList<GameEntity> GetByTag(string codeName)
        {
            int id = TagManager.Instance?.GetId(codeName) ?? -1;
            if (id != -1 && _entitiesByTag.TryGetValue(id, out var list))
            {
                return list;
            }
            return new List<GameEntity>();
        }
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

            // Direct 1-pass WVP projection: World -> Viewport (A x B) -> Clip Space
            _basicEffect.World = Matrix.Identity;
            _basicEffect.View = Matrix.Identity;
            _basicEffect.Projection = camera.SimWVP;
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
        public void DrawAlbedoPass(GraphicsDevice gd, Camera camera, bool enableParallax, float parallaxStrength, List<RenderableSprite> renderList, WeatherSimulator weather = null, GameTime gameTime = null)
        {
            if (renderList.Count == 0) return;
            SetupRenderState(gd, camera);

            float totalSec = gameTime != null ? (float)gameTime.TotalGameTime.TotalSeconds : 0f;
            WindWaveSample wind = weather != null
                ? weather.SampleWindWave(totalSec, 12.0f)
                : new WindWaveSample { Direction = new Vector2(1, 0), Velocity = 2.5f, GustAmplitude = 6f, TimeStepped = totalSec };

            int spriteCount = 0;
            Texture2D currentTexture = null;

            foreach (var sprite in renderList)
            {
                if (sprite.HasWindSway && sprite.Rotation == 0f)
                {
                    int stripH = Math.Max(1, sprite.StripHeight);
                    int totalH = sprite.SourceRect.Height;
                    float pivotY = sprite.Origin.Y * sprite.Scale.Y;

                    for (int y = 0; y < totalH; y += stripH)
                    {
                        if (spriteCount >= MAX_SPRITES || (currentTexture != null && currentTexture != sprite.Texture))
                        {
                            FlushBatch(gd, _basicEffect, currentTexture, spriteCount);
                            spriteCount = 0;
                        }
                        currentTexture = sprite.Texture;

                        int currentStripH = Math.Min(stripH, totalH - y);
                        float localY = y * sprite.Scale.Y;

                        // Calculate displacement with map-wide propagation and decorrelation
                        float horizontalShift = CalculateStripDisplacement(
                            sprite.Position, localY, pivotY,
                            sprite.Stiffness, sprite.WaveLength, sprite.FlutterStr, wind);

                        // Strip quad corner positions
                        float left = -sprite.Origin.X * sprite.Scale.X + horizontalShift;
                        float right = (sprite.SourceRect.Width - sprite.Origin.X) * sprite.Scale.X + horizontalShift;
                        float top = -sprite.Origin.Y * sprite.Scale.Y + localY;
                        float bottom = top + (currentStripH * sprite.Scale.Y);

                        Vector2 tl = new Vector2(sprite.Position.X + left, sprite.Position.Y + top);
                        Vector2 tr = new Vector2(sprite.Position.X + right, sprite.Position.Y + top);
                        Vector2 bl = new Vector2(sprite.Position.X + left, sprite.Position.Y + bottom);
                        Vector2 br = new Vector2(sprite.Position.X + right, sprite.Position.Y + bottom);

                        float texW = 1f / currentTexture.Width;
                        float texH = 1f / currentTexture.Height;
                        float u0 = (sprite.SourceRect.X * texW) + (0.1f * texW);
                        float u1 = ((sprite.SourceRect.Right) * texW) - (0.1f * texW);
                        float v0 = ((sprite.SourceRect.Y + y) * texH) + (0.1f * texH);
                        float v1 = ((sprite.SourceRect.Y + y + currentStripH) * texH) - (0.1f * texH);

                        int vIdx = spriteCount * 4;
                        _vertices[vIdx + 0] = new VertexPositionColorTexture(new Vector3(tl, 0), Color.White, new Vector2(u0, v0));
                        _vertices[vIdx + 1] = new VertexPositionColorTexture(new Vector3(tr, 0), Color.White, new Vector2(u1, v0));
                        _vertices[vIdx + 2] = new VertexPositionColorTexture(new Vector3(bl, 0), Color.White, new Vector2(u0, v1));
                        _vertices[vIdx + 3] = new VertexPositionColorTexture(new Vector3(br, 0), Color.White, new Vector2(u1, v1));
                        spriteCount++;
                    }
                }
                else
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
        public void DrawYDepthPass(GraphicsDevice gd, Camera camera, bool enableParallax, float parallaxStrength, List<RenderableSprite> renderList)
        {
            if (renderList.Count == 0) return;

            // Write strictly to Red channel (Height) and Alpha
            gd.BlendState = new BlendState
            {
                ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Alpha,
                ColorSourceBlend = Blend.SourceAlpha,
                ColorDestinationBlend = Blend.InverseSourceAlpha,
                AlphaSourceBlend = Blend.One,
                AlphaDestinationBlend = Blend.Zero
            };
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.SamplerStates[0] = SamplerState.PointClamp;

            SetupRenderState(gd, camera);

            const float MAX_WORLD_ALTITUDE = 350f; // Baseline altitude range for fog
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

                float texW = 1f / currentTexture.Width;
                float texH = 1f / currentTexture.Height;
                float u0 = (sprite.SourceRect.X * texW) + (0.1f * texW);
                float v0 = (sprite.SourceRect.Y * texH) + (0.1f * texH);
                float u1 = (sprite.SourceRect.Right * texW) - (0.1f * texW);
                float v1 = (sprite.SourceRect.Bottom * texH) - (0.1f * texH);

                // Height in world pixels
                float physicalHeight = sprite.SourceRect.Height * sprite.Scale.Y;

                // Top vertices = Maximum altitude of this sprite above ground
                byte topRed = (byte)MathHelper.Clamp((physicalHeight / MAX_WORLD_ALTITUDE) * 255f, 0f, 255f);

                // Red = Altitude, Green/Blue = 0 (reserved for future use)
                Color topColor = new Color(topRed, (byte)0, (byte)0, (byte)255);
                Color botColor = new Color((byte)0, (byte)0, (byte)0, (byte)255); // Ground contact = 0 altitude

                int vIdx = spriteCount * 4;
                _vertices[vIdx + 0] = new VertexPositionColorTexture(new Vector3(tl, 0), topColor, new Vector2(u0, v0));
                _vertices[vIdx + 1] = new VertexPositionColorTexture(new Vector3(tr, 0), topColor, new Vector2(u1, v0));
                _vertices[vIdx + 2] = new VertexPositionColorTexture(new Vector3(bl, 0), botColor, new Vector2(u0, v1));
                _vertices[vIdx + 3] = new VertexPositionColorTexture(new Vector3(br, 0), botColor, new Vector2(u1, v1));
                spriteCount++;
            }
            if (spriteCount > 0) FlushBatch(gd, _basicEffect, currentTexture, spriteCount);
        }

        public void DrawZDepthPass(GraphicsDevice gd, Camera camera, Effect zDepthShader, bool enableParallax, float parallaxStrength, List<RenderableSprite> renderList)
        {
            if (renderList.Count == 0 || zDepthShader == null) return;

            // Write strictly to Red channel (Screen depth) and Alpha
            gd.BlendState = new BlendState
            {
                ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Alpha,
                ColorSourceBlend = Blend.One,
                ColorDestinationBlend = Blend.Zero
            };
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.SamplerStates[0] = SamplerState.PointClamp;

            zDepthShader.Parameters["MatrixTransform"]?.SetValue(camera.ScreenWVP);

            float camTop = camera.SimViewBounds.Top;
            float camHeight = camera.SimViewBounds.Height;

            int spriteCount = 0;
            Texture2D currentTexture = null;

            foreach (var sprite in renderList)
            {
                if (spriteCount >= MAX_SPRITES || (currentTexture != null && currentTexture != sprite.Texture))
                {
                    FlushBatch(gd, zDepthShader, currentTexture, spriteCount);
                    spriteCount = 0;
                }
                currentTexture = sprite.Texture;

                CalculateSpriteGeometry(sprite, camera, enableParallax, parallaxStrength, out Vector2 tl, out Vector2 tr, out Vector2 bl, out Vector2 br);

                float texW = 1f / currentTexture.Width;
                float texH = 1f / currentTexture.Height;
                float u0 = (sprite.SourceRect.X * texW) + (0.1f * texW);
                float v0 = (sprite.SourceRect.Y * texH) + (0.1f * texH);
                float u1 = (sprite.SourceRect.Right * texW) - (0.1f * texW);
                float v1 = (sprite.SourceRect.Bottom * texH) - (0.1f * texH);

                // Screen-space sorting depth: 0.001 (top of screen) to 1.0 (bottom of screen)
                float screenDepth = DepthUtil.CalculateScreenDepth(sprite.BaseWorldY, camTop, camHeight);
                byte depthByte = (byte)(screenDepth * 255f);

                // Red = Sorting depth, Green/Blue = 0 (reserved for future use)
                Color depthColor = new Color(depthByte, (byte)0, (byte)0, (byte)255);

                int vIdx = spriteCount * 4;
                _vertices[vIdx + 0] = new VertexPositionColorTexture(new Vector3(tl, 0f), depthColor, new Vector2(u0, v0));
                _vertices[vIdx + 1] = new VertexPositionColorTexture(new Vector3(tr, 0f), depthColor, new Vector2(u1, v0));
                _vertices[vIdx + 2] = new VertexPositionColorTexture(new Vector3(bl, 0f), depthColor, new Vector2(u0, v1));
                _vertices[vIdx + 3] = new VertexPositionColorTexture(new Vector3(br, 0f), depthColor, new Vector2(u1, v1));
                spriteCount++;
            }

            if (spriteCount > 0) FlushBatch(gd, zDepthShader, currentTexture, spriteCount);
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

        public static float CalculateStripDisplacement(
           Vector2 worldPos,
           float localY,
           float pivotY,
           float stiffness,
           float waveLength,
           float flutterStr,
           WindWaveSample wind)
        {
            // Slices at or below the ground contact pivot remain anchored (0 displacement)
            if (localY >= pivotY) return 0f;

            float normHeight = (pivotY - localY) / Math.Max(1f, pivotY);
            float flex = MathF.Pow(normHeight, 1.6f) * (1.0f - stiffness * 0.75f);

            // 1. MAP-WIDE WAVE PROPAGATION:
            // Project the object's world position along the wind direction vector.
            // As the wind wave washes across the map, trees upwind bend before trees downwind.
            float spatialCoord = Vector2.Dot(worldPos, wind.Direction);

            // 2. Wave phase calculation
            float wavePhase = ((spatialCoord + (localY * 0.35f)) / Math.Max(waveLength, 1.0f)) - (wind.TimeStepped * wind.Velocity);

            // 3. PER-OBJECT DESYNCHRONIZATION (Jitter Hash):
            // Prevents adjacent trees from oscillating in mechanical synchrony.
            float objHash = Hash(worldPos.X, worldPos.Y);
            float phaseJitter = (objHash - 0.5f) * 1.5f;

            // 4. Harmonic components: Gust wave + High-frequency leaf tremor
            float gust = MathF.Sin(wavePhase + phaseJitter) * wind.GustAmplitude;
            float microFlutter = MathF.Sin(wind.TimeStepped * 7.5f + (localY * 0.15f) + (objHash * 6.283f)) * flutterStr * 4.0f;

            // Discrete integer pixel grid snapping for rock-solid pixel art
            return MathF.Round((gust + microFlutter) * flex);
        }

        private static float Hash(float x, float y)
        {
            float n = MathF.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return n - MathF.Floor(n);
        }
    }
}