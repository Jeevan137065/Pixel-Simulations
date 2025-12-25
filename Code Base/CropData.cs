using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Pixel_Simulations
{

    public static class GameConstants
    {
        public const int GridCellSize = 16;
    }

    // NEW: A reusable struct to hold the grid-based sprite data
    public class SpriteGridData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int WidthInCells { get; set; }
        public int HeightInCells { get; set; }
    }

    public class CropStageData
    {
        // UPDATED: Replaced SpriteX/Y with this new object
        public SpriteGridData Sprite { get; set; }
        public bool IsHarvestable { get; set; }
    }

    public enum SowingMethod
    {
        Normal,             // One plant, centered.
        InterplantHorizontal, // Two different plants, side-by-side.
        InterplantCorners     // One primary plant in the center, secondary at corners.
    }

    public class PlantingPlot
    {
        public int TileX { get; }
        public int TileY { get; }
        public SowingMethod SowingMethod { get; set; }
        public List<Crop> Crops { get; }

        public PlantingPlot(int tileX, int tileY, SowingMethod method)
        {
            TileX = tileX;
            TileY = tileY;
            SowingMethod = method;
            Crops = new List<Crop>();
        }
    }
        
    public class CropData
    {
        public string ID { get; set; }
        public Tool SeedTool { get; set; }
        public List<Season> Seasons { get; set; }
        public int GerminationDays { get; set; }    // g
        public int GerminationSprites { get; set; } // t
        public int GrowthDaysPerStage { get; set; } // G
        public int GrowthSprites { get; set; }      // T
        public int RipeDays { get; set; }           // R
        public int FootprintWidth { get; set; }
        public int FootprintHeight { get; set; }
        public List<CropStageData> Stages { get; set; }

        public SpriteGridData SeedIcon { get; set; }
        public SpriteGridData HarvestIcon { get; set; }
    }
    public class Crop : IRenderable, ISwayable
    {
        private enum GrowthState { Seed, Germinating, Growing, Ripe, Rotten }
        public SwayType SwayMode { get; set; } = SwayType.TriangleWave;
        public CropData Data { get; }
        public Vector2 Position { get; }
        private Vector2 _drawPosition;
        public float Depth => Position.Y + _sourceRect.Height;
        public bool IsHarvestable => Data.Stages[_currentStageIndex].IsHarvestable;
        private GraphicsDevice _gd;
        // Growth State
        private GrowthState _currentState;
        private int _daysInCurrentPhase;
        private int _currentStageIndex;

        // Drawing
        private readonly Texture2D _texture;
        private readonly Texture2D _normalTexture;
        private Rectangle _sourceRect;
        private Vector2 _origin;
        private VertexPositionTexture[] _vertices = new VertexPositionTexture[4];
        private VertexBuffer _vertexBuffer;

        public Rectangle Bounds => new Rectangle((int)(Position.X - 4),(int)(Position.Y - 8),8, 8);
        public float _swayValue { get; private set; }
        private const float MaxSway = 1f;     // Maximum sway in pixels.
        private const float SwayStiffness = 20f; // How strong the "spring" is. Higher = faster oscillation.
        private const float SwayDamping = 20f;     // How quickly the oscillation dies down. Higher = less oscillation.
        private const float SwayMass = 10f;        // The "weight" of the plant. Higher = slower to react.
        private float _swayVelocity = 0.1f;                 // The current speed of the sway

        private const float ShakeDecayRate = 0.8f; // How much maxShake decreases per second.
        private const float ShakeRateMultiplier = 12f; // How fast it oscillates.

        private bool _shakeLeft;
        private float _shakeRotation; // This is now our primary sway value, directly in pixels.
        private float _maxShake;
        private double _lastPushTime;
        public Crop(CropData data, Vector2 position, Texture2D texture, Texture2D normalTexture, GraphicsDevice gd)
        {

            Data = data;
            Position = position;
            _texture = texture;
            _normalTexture = normalTexture;
            _currentState = GrowthState.Seed;
            _daysInCurrentPhase = 0;
            _currentStageIndex = 0; // The very first sprite in the list
            _gd = gd;
            _vertexBuffer = new VertexBuffer(_gd, typeof(VertexPositionTexture), 4, BufferUsage.WriteOnly);
            UpdateSprite();
        }

        private void UpdateSprite()
        {
            var stageData = Data.Stages[_currentStageIndex];
            var spriteData = stageData.Sprite;

            // The core logic: convert grid coordinates to pixel rectangle
            _sourceRect = new Rectangle(
                spriteData.X * GameConstants.GridCellSize,
                spriteData.Y * GameConstants.GridCellSize,
                spriteData.WidthInCells * GameConstants.GridCellSize,
                spriteData.HeightInCells * GameConstants.GridCellSize
            );

            // The origin is also dynamic, always the bottom-center of the calculated sprite
            _origin = new Vector2(_sourceRect.Width / 2f, _sourceRect.Height);
            //_origin = new Vector2(_sourceRect.Width - GameConstants.GridCellSize, _sourceRect.Height-GameConstants.GridCellSize);
            _drawPosition = new Vector2(
            (int)Math.Round(Position.X - _origin.X),
            (int)Math.Round(Position.Y - _origin.Y)
        );

            Vector2 texTopLeft = new Vector2((float)_sourceRect.X / _texture.Width, (float)_sourceRect.Y / _texture.Height);
            Vector2 texBottomRight = new Vector2((float)_sourceRect.Right / _texture.Width, (float)_sourceRect.Bottom / _texture.Height);

            _vertices[0] = new VertexPositionTexture(new Vector3(_drawPosition.X, _drawPosition.Y, 0), texTopLeft); // Top-Left
            _vertices[1] = new VertexPositionTexture(new Vector3(_drawPosition.X + _sourceRect.Width, _drawPosition.Y, 0), new Vector2(texBottomRight.X, texTopLeft.Y)); // Top-Right
            _vertices[2] = new VertexPositionTexture(new Vector3(_drawPosition.X, _drawPosition.Y + _sourceRect.Height, 0), new Vector2(texTopLeft.X, texBottomRight.Y)); // Bottom-Left
            _vertices[3] = new VertexPositionTexture(new Vector3(_drawPosition.X + _sourceRect.Width, _drawPosition.Y + _sourceRect.Height, 0), texBottomRight); // Bottom-Right

            //_origin = new Vector2((_sourceRect.Width - GameConstants.GridCellSize)/2, (_sourceRect.Height - GameConstants.GridCellSize)/2);
        }

        public void Push(Vector2 direction, float force)
        {
            // 1. If the player is barely moving, don't apply force.
            if (direction.LengthSquared() < 0.01f) return;

            // 2. COOLDOWN CHECK: Don't accept a push if we were pushed recently (e.g., last 0.5 seconds)
            // This prevents the "machine gun" effect of adding force every single frame (60 times a second).
            // We use the system time or we can pass GameTime to Push. 
            // A simpler check is: "Is it already swaying hard in that direction?"

            if (SwayMode == SwayType.TriangleWave)
            {
                // For Triangle wave, we just reset the max shake, so it handles itself better.
                // But we can prevent resetting if it's already high.
                if (Math.Abs(_maxShake) > force * 0.8f) return;

                _shakeLeft = direction.X < 0;
                _maxShake = Math.Min(12f, force);
            }
            else // Physics Spring
            {
                // For physics, adding velocity every frame explodes the simulation.
                // CHECK: If we are already swaying in this direction, don't add more force.
                bool pushingLeft = direction.X < 0;
                bool movingLeft = _swayVelocity < 0;

                if (pushingLeft == movingLeft && Math.Abs(_swayValue) > 5.0f)
                {
                    // We are already swung out, don't add more energy
                    return;
                }

                _swayVelocity += direction.X * force;
            }
        }

        public void UpdateSway(GameTime gameTime)
        {
            switch (SwayMode)
            {
                case SwayType.PhysicsSpring:
                    UpdateSwayPhysics(gameTime);
                    break;
                case SwayType.TriangleWave:
                    UpdateSwayTriangle(gameTime);
                    break;
            }
        }

        // METHOD A: Spring Physics (Elastic)
        private void UpdateSwayPhysics(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (elapsed == 0) return;

            // Hooke's Law + Damping
            float springForce = -350f * _swayValue; // Stiffness
            float dampingForce = -12f * _swayVelocity; // Damping
            float acceleration = (springForce + dampingForce) / 1.0f; // Mass

            _swayVelocity += acceleration * elapsed;
            _swayValue += _swayVelocity * elapsed;

            // Clamp
            _swayValue = MathHelper.Clamp(_swayValue, -14f, 14f);

            // Sleep check
            if (Math.Abs(_swayValue) < 0.01f && Math.Abs(_swayVelocity) < 0.01f)
            {
                _swayValue = 0;
                _swayVelocity = 0;
            }
        }

        // METHOD B: Triangle Wave (Linear Decay)
        private void UpdateSwayTriangle(GameTime gameTime)
        {
            // Re-use the logic we wrote in the previous step
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            // ... (Insert the triangle wave logic here from previous response) ...
            // For brevity, I assume you have the _shakeLeft, _maxShake logic here.
            // Let's ensure the variables match:
            if (_maxShake > 0)
            {
                float shakeRate = _maxShake * 12f * elapsed;
                if (_shakeLeft)
                {
                    _swayValue -= shakeRate; // Using _swayValue as rotation
                    if (_swayValue <= -_maxShake) _shakeLeft = false;
                }
                else
                {
                    _swayValue += shakeRate;
                    if (_swayValue >= _maxShake) _shakeLeft = true;
                }
                _maxShake = Math.Max(0f, _maxShake - (0.8f * (1 + _maxShake * 0.25f) * elapsed));
            }
            else if (Math.Abs(_swayValue) > 0.1f) { _swayValue *= 0.9f; }
            else { _swayValue = 0; }
        }

        public void UpdateVertices(float totalTime, float windAmount, float windSpeed)
        {
            // 1. Calculate the wind sway
            float windSway = (float)Math.Sin(totalTime * windSpeed + Position.X * 0.1f) * windAmount;

            // 2. Combine with player-induced sway
            float totalSway = _swayValue + windSway;

            // 3. Create the curved bend effect using pow()
            float topSway = (float)Math.Pow(1.0f, 1.5) * totalSway; // Top vertices get 100% of the sway
                                                                    // float midSway = (float)Math.Pow(0.5f, 1.5) * totalSway; // Middle vertices would get less (not needed for quad)

            // 4. Update the X position of the top two vertices
            _vertices[0].Position.X = _drawPosition.X + topSway;
            _vertices[1].Position.X = _drawPosition.X + _sourceRect.Width + topSway;

            // 5. Update the GPU with the new vertex data
            _vertexBuffer.SetData(_vertices);
        }

        // This is the core growth logic
        public void AdvanceDay()
        {
            if (_currentState == GrowthState.Rotten) return; // Cannot grow further

            _daysInCurrentPhase++;

            switch (_currentState)
            {
                case GrowthState.Seed:
                    // The seed stage lasts for 1 day (the day it's planted).
                    // On the next day, it transitions to Germinating.
                    if (_daysInCurrentPhase >= 1)
                    {
                        _currentState = GrowthState.Germinating;
                        _daysInCurrentPhase = 0; // Reset for the new phase
                        _currentStageIndex = 1;  // Move to the first germination sprite
                        UpdateSprite();
                    }
                    break;

                case GrowthState.Germinating:
                    // Check if the overall germination period is complete.
                    if (_daysInCurrentPhase >= Data.GerminationDays)
                    {
                        _currentState = GrowthState.Growing;
                        _daysInCurrentPhase = 0;
                        // The first growth sprite comes after the seed (1) and all germination sprites (t)
                        _currentStageIndex = 1 + Data.GerminationSprites;
                        UpdateSprite();
                    }
                    else
                    {
                        // Update sprite within the germination phase.
                        // This calculates which of the 't' sprites should be shown based on progress.
                        int expectedSpriteSubIndex = (int)Math.Floor((double)_daysInCurrentPhase / Data.GerminationDays * Data.GerminationSprites);
                        int newStageIndex = 1 + expectedSpriteSubIndex;

                        if (newStageIndex != _currentStageIndex)
                        {
                            _currentStageIndex = newStageIndex;
                            UpdateSprite();
                        }
                    }
                    break;

                case GrowthState.Growing:
                    int totalGrowthDays = Data.GrowthDaysPerStage * Data.GrowthSprites;
                    // Check if the overall growth period is complete.
                    if (_daysInCurrentPhase >= totalGrowthDays)
                    {
                        _currentState = GrowthState.Ripe;
                        _daysInCurrentPhase = 0;
                        // No sprite change needed, it's already at the final mature stage.
                    }
                    else
                    {
                        // Update sprite within the growth phase.
                        // A new growth sprite appears every 'G' days.
                        int expectedSpriteSubIndex = (int)Math.Floor((double)_daysInCurrentPhase / Data.GrowthDaysPerStage);
                        int newStageIndex = 1 + Data.GerminationSprites + expectedSpriteSubIndex;

                        if (newStageIndex != _currentStageIndex)
                        {
                            _currentStageIndex = newStageIndex;
                            UpdateSprite();
                        }
                    }
                    break;

                case GrowthState.Ripe:
                    // Check if the ripe period is over.
                    if (_daysInCurrentPhase >= Data.RipeDays)
                    {
                        _currentState = GrowthState.Rotten;
                        _daysInCurrentPhase = 0;
                        // The rotten sprite is the very last one in the list.
                        _currentStageIndex = Data.Stages.Count - 1;
                        UpdateSprite();
                    }
                    break;
            }
        }


        public void Draw(BasicEffect effect, IndexBuffer indexBuffer)
        {
            // Tell the effect which texture to use
            effect.Texture = _texture;
            effect.TextureEnabled = true;

            // Set the vertex and index buffers on the GPU
            _gd.SetVertexBuffer(_vertexBuffer);
            _gd.Indices = indexBuffer;
            // Apply the shader and draw the quad
            // Apply the effect and draw
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        }

        public void DrawNormal(SpriteBatch spriteBatch, Effect normalEffect, IndexBuffer indexBuffer)
        {
            // Set the specific normal map for this object
            normalEffect.Parameters["NormalTexture"].SetValue(_normalTexture);

            // Vertices are already updated, just draw
            // Note: We use a custom Effect, not BasicEffect here
            _gd.SetVertexBuffer(_vertexBuffer);
            _gd.Indices = indexBuffer;

            foreach (var pass in normalEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        }
        public void DrawDebugOutline( BasicEffect effect)
        {
            var debugVertices = new VertexPositionColor[5]; // 5 points to close the loop

            // Use the actual _vertices positions that are updated by physics
            debugVertices[0] = new VertexPositionColor(_vertices[0].Position, Color.Magenta); // TL
            debugVertices[1] = new VertexPositionColor(_vertices[1].Position, Color.Magenta); // TR
            debugVertices[2] = new VertexPositionColor(_vertices[3].Position, Color.Magenta); // BR
            debugVertices[3] = new VertexPositionColor(_vertices[2].Position, Color.Magenta); // BL
            debugVertices[4] = new VertexPositionColor(_vertices[0].Position, Color.Magenta); // Back to TL to close loop

            effect.TextureEnabled = false;
            effect.VertexColorEnabled = true;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                // Draw the line strip
                _gd.DrawUserPrimitives(PrimitiveType.LineStrip, debugVertices, 0, 4);
            }
        }

        public void DrawDepth(SpriteBatch spriteBatch, Effect depthEffect, IndexBuffer indexBuffer)
        {
            // 1. Set Texture
            depthEffect.Parameters["SpriteTexture"].SetValue(_texture);

            // 2. Set Buffers
            _gd.SetVertexBuffer(_vertexBuffer);
            _gd.Indices = indexBuffer;

            // 3. Draw Quad
            foreach (var pass in depthEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // We will need a reference to the texture, passed in from WorldMap or a central asset manager
            spriteBatch.Draw(_texture, _drawPosition, _sourceRect, Color.White);
        }
    }
}
