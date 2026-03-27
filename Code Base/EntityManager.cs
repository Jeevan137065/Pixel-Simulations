using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Pixel_Simulations.Data;

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

    public class EntityManager
    {
        private readonly List<GameEntity> _allEntities = new List<GameEntity>();
        private readonly Dictionary<string, GameEntity> _entitiesById = new Dictionary<string, GameEntity>();
        private readonly Dictionary<string, List<GameEntity>> _entitiesByTag = new Dictionary<string, List<GameEntity>>();

        public IReadOnlyList<GameEntity> AllEntities => _allEntities;

        public void LoadFromMap(Map map, PrefabManager prefabManager)
        {
            Clear();
            foreach (var layer in map.Layers)
            {
                if (layer is ObjectLayer objLayer)
                {
                    foreach (var obj in objLayer.Objects)
                    {
                        RegisterObject(obj, prefabManager);
                    }
                }
                if (layer is ControlLayer ctrlLayer)
                {
                    foreach (var rect in ctrlLayer.Rectangles) RegisterObject(rect, prefabManager);
                    foreach (var shape in ctrlLayer.Shapes) RegisterObject(shape, prefabManager);
                    foreach (var pt in ctrlLayer.Points) RegisterObject(pt, prefabManager);
                }
            }
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
    }
}