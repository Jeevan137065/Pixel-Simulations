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
    public class Crop : IRenderable
    {
        private enum GrowthState { Seed, Germinating, Growing, Ripe, Rotten }

        public CropData Data { get; }
        public Vector2 Position { get; }
        private Vector2 _drawPosition;
        public float Depth => Position.Y + _sourceRect.Height;
        public bool IsHarvestable => Data.Stages[_currentStageIndex].IsHarvestable;

        // Growth State
        private GrowthState _currentState;
        private int _daysInCurrentPhase;
        private int _currentStageIndex;

        // Drawing
        private readonly Texture2D _texture;
        private Rectangle _sourceRect;
        private Vector2 _origin;


        public Crop(CropData data, Vector2 position, Texture2D texture)
        {

            Data = data;
            Position = position;
            _texture = texture;
            _currentState = GrowthState.Seed;
            _daysInCurrentPhase = 0;
            _currentStageIndex = 0; // The very first sprite in the list
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
            //_origin = new Vector2((_sourceRect.Width - GameConstants.GridCellSize)/2, (_sourceRect.Height - GameConstants.GridCellSize)/2);
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


        public void Draw(SpriteBatch spriteBatch)
        {
           
            // We will need a reference to the texture, passed in from WorldMap or a central asset manager
            spriteBatch.Draw(_texture, _drawPosition, _sourceRect, Color.White);
        }
    }
}
