using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Pixel_Simulations
{ 
    public class WorldMap
    {
        // Textures
        private Texture2D _grassTilesTexture;
        private Texture2D _grassPlantsTexture;
        private Texture2D _treeTrunkTexture;

        // Source Rectangles
        private readonly Rectangle[] _grassTileSources = new Rectangle[4];
        private readonly Rectangle[] _grassPlantSources = new Rectangle[4];
        private readonly Rectangle[] _treeSources = new Rectangle[2];

        // Map Data
        private Rectangle[,] _tileGrid;
        private bool[,] _isTileOccupied; // To prevent spawning plants/trees on top of each other

        // Public lists of all dynamic, renderable objects on the map
        public List<Tree> Trees { get; private set; }
        public List<Decoration> Decorations { get; private set; }

        // World Properties
        private readonly int _worldWidth, _worldHeight, _tileSize;
        private readonly int _worldWidthInTiles, _worldHeightInTiles;
        private readonly Random _random = new();

        public WorldMap(int worldWidth, int worldHeight, int tileSize)
        {
            _worldWidth = worldWidth;
            _worldHeight = worldHeight;
            _tileSize = tileSize;
            _worldWidthInTiles = worldWidth / tileSize;
            _worldHeightInTiles = worldHeight / tileSize;
        }

        public void LoadContent(ContentManager content)
        {
            // Load textures
            _grassTilesTexture = content.Load<Texture2D>("Grass1");
            _grassPlantsTexture = content.Load<Texture2D>("GrassBlade");
            _treeTrunkTexture = content.Load<Texture2D>("Trunk1");

            // Initialize lists
            Trees = new List<Tree>();
            Decorations = new List<Decoration>();

            // Pre-calculate source rectangles
            for (int i = 0; i < 4; i++) _grassTileSources[i] = new Rectangle(0, i * 16, 16, 16);
            for (int i = 0; i < 4; i++) _grassPlantSources[i] = new Rectangle(0, i * 24, 24, 24);
            for (int i = 0; i < 2; i++) _treeSources[i] = new Rectangle(i * 64, 0, 64, 320); // 2 trunks in a 128x320 image

            GenerateWorld();
        }

        private void GenerateWorld()
        {
            _tileGrid = new Rectangle[_worldWidthInTiles, _worldHeightInTiles];
            _isTileOccupied = new bool[_worldWidthInTiles, _worldHeightInTiles];
            const float noiseScale = 0.1f;

            // --- PASS 1: Place large objects like trees ---
            var acceptedTreePositions = GenerateTreePositions();
            foreach (var treeHotspot in acceptedTreePositions)
            {
                PlaceTree(treeHotspot);
            }


            // --- PASS 2: Place ground tiles and small decorations ---
            for (int y = 0; y < _worldHeightInTiles; y++)
            {
                for (int x = 0; x < _worldWidthInTiles; x++)
                {
                    // Place ground tile everywhere
                    double noiseValue = Perlin.Noise(x * noiseScale, y * noiseScale);
                    int greennessIndex = Math.Min(3, (int)(noiseValue * 4));
                    _tileGrid[x, y] = _grassTileSources[greennessIndex];

                    // Place decorations only on non-occupied tiles
                    if (!_isTileOccupied[x, y] && _random.NextDouble() < 0.15) // 15% chance
                    {
                        var plantPos = new Vector2(x * _tileSize - 4, y * _tileSize - 4);
                        var newDeco = new Decoration(_grassPlantsTexture, plantPos, _grassPlantSources[_random.Next(0, 4)]);
                        Decorations.Add(newDeco);
                    }
                }
            }
        }

        private void PlaceTree(Vector2 treeHotspot)
        {
            Rectangle treeSource = _treeSources[_random.Next(0, 2)];

            // Position the tree's top-left corner based on its hotspot
            var treePos = new Vector2(
                treeHotspot.X - (treeSource.Width / 2f),
                treeHotspot.Y - treeSource.Height
            );

            Trees.Add(new Tree(_treeTrunkTexture, treePos, treeSource));

            // Mark the tile(s) under the tree trunk base as occupied
            int tileX = (int)(treeHotspot.X / _tileSize);
            int tileY = (int)(treeHotspot.Y / _tileSize);
            int tilesUnderTree = (int)Math.Ceiling(treeSource.Width / (float)_tileSize);
            int startX = tileX - tilesUnderTree / 2;

            for (int i = 0; i < tilesUnderTree; i++)
            {
                int currentX = startX + i;
                if (currentX >= 0 && currentX < _worldWidthInTiles && tileY >= 0 && tileY < _worldHeightInTiles)
                {
                    _isTileOccupied[currentX, tileY] = true;
                }
            }
        }

        private List<Vector2> GenerateTreePositions()
        {
            var candidatePositions = new List<Vector2>();

            // 1. Generate a list of all possible candidate positions
            for (int y = 0; y < _worldHeightInTiles; y++)
            {
                for (int x = 0; x < _worldWidthInTiles; x++)
                {
                    // Use a higher chance here because we will filter most of them out
                    if (_random.NextDouble() < 0.10) // 10% chance per tile to be a candidate
                    {
                        var hotspot = new Vector2(
                            x * _tileSize + (_tileSize / 2f),
                            y * _tileSize + (_tileSize / 2f)
                        );
                        candidatePositions.Add(hotspot);
                    }
                }
            }

            // 2. Shuffle the candidates to ensure random, non-biased placement
            candidatePositions.Shuffle();

            var acceptedPositions = new List<Vector2>();
            var minHorizontalDist = 64; // Minimum horizontal gap between tree centers
            var minVerticalDist = 360; // Minimum vertical distance if in the same "column"

            // 3. Filter the candidates based on our spacing rules
            foreach (var candidate in candidatePositions)
            {
                bool isValid = true;
                foreach (var accepted in acceptedPositions)
                {
                    float dx = Math.Abs(candidate.X - accepted.X);
                    float dy = Math.Abs(candidate.Y - accepted.Y);

                    // Rule 1: No tree can be too close horizontally.
                    // This is the primary rule for creating paths.
                    if (dx < minHorizontalDist)
                    {
                        isValid = false;
                        break;
                    }

                    // Rule 2: If trees are in the same rough vertical "column", they must be very far apart.
                    // This prevents trees from spawning directly behind one another.
                    // We consider them in the same column if the horizontal distance is less than double the minimum.
                    if (dx < minHorizontalDist * 2 && dy < minVerticalDist)
                    {
                        isValid = false;
                        break;
                    }
                }

                if (isValid)
                {
                    acceptedPositions.Add(candidate);
                }
            }
            return acceptedPositions;
        }
        public void DrawGround(SpriteBatch spriteBatch, Rectangle cameraView)
        {
            int startX = Math.Max(0, cameraView.X / _tileSize);
            int startY = Math.Max(0, cameraView.Y / _tileSize);
            int endX = Math.Min(_worldWidthInTiles - 1, (cameraView.Right / _tileSize) + 1);
            int endY = Math.Min(_worldHeightInTiles - 1, (cameraView.Bottom / _tileSize) + 1);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    var destRect = new Rectangle(x * _tileSize, y * _tileSize, _tileSize, _tileSize);
                    spriteBatch.Draw(_grassTilesTexture, destRect, _tileGrid[x, y], Color.White);
                }
            }
        }
    }
}