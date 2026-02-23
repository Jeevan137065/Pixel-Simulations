using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations
{
    // This is a complex class. Here is a scaffold of its structure.
    // Represents a single frame in an animation, mapping body parts to their coordinates on the spritesheet.
    public class AnimationFrame
    {
        public Dictionary<string, Point> Parts { get; set; } = new Dictionary<string, Point>();
    }

    // Represents a complete animation sequence, like "Walk_East".
    public class Animation
    {
        public List<AnimationFrame> Frames { get; set; } = new List<AnimationFrame>();
        public float FrameDuration { get; set; } = 0.1f; // Default duration in seconds
        public bool IsLooping { get; set; } = true;
    }

    // This is the top-level class that matches the structure of our JSON file.
    public class AnimationFile
    {
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public Dictionary<string, Animation> Animations { get; set; } = new Dictionary<string, Animation>();
        public Dictionary<string, Dictionary<string, int>> DrawOrder { get; set; } = new Dictionary<string, Dictionary<string, int>>();
    }
    public class AnimationManager
    {
        public Texture2D SpriteSheet { get; private set; }
        private AnimationFile _animationFile;
        private string _currentAnimationKey;
        private Animation _currentAnimation;
        private float _frameTimer;
        private int _currentFrameIndex;

        public void LoadContent(ContentManager content, string textureAsset, string jsonAsset)
        {
            SpriteSheet = content.Load<Texture2D>(textureAsset);
            string jsonPath = Path.Combine(content.RootDirectory, jsonAsset);
            string json = File.ReadAllText(jsonPath);
            _animationFile = JsonConvert.DeserializeObject<AnimationFile>(json);
        }

        public void Update(GameTime gameTime, PlayerState state, Direction direction, Direction previousDirection, bool isTurning)
        {
            string animKey = GetAnimationKey(state, direction, previousDirection, isTurning);

            if (_currentAnimationKey != animKey)
            {
                SetCurrentAnimation(animKey);
            }

            if (_currentAnimation == null) return;

            // Update the frame timer
            _frameTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_frameTimer >= _currentAnimation.FrameDuration)
            {
                _frameTimer -= _currentAnimation.FrameDuration;
                _currentFrameIndex++;
                if (_currentFrameIndex >= _currentAnimation.Frames.Count)
                {
                    _currentFrameIndex = _currentAnimation.IsLooping ? 0 : _currentAnimation.Frames.Count - 1;
                }
            }
        }

        private string GetAnimationKey(PlayerState state, Direction direction, Direction previousDirection, bool isTurning)
        {
            if (isTurning)
            {
                // Simple turn animation logic
                return $"Turn_{previousDirection}_{direction}";
            }

            if (state == PlayerState.Walking)
            {
                // Use Idle for up/down walking, Walk for left/right
                if (direction == Direction.East || direction == Direction.West) return $"Walk_East";
                if (direction == Direction.North) return $"Idle_North";
                return $"Idle_South";
            }

            // Default to Idle
            return $"Idle_{direction}";
        }
        public void SetCurrentAnimation(string animKey)
        {
            if (_animationFile.Animations.TryGetValue(animKey, out var newAnimation))
            {
                _currentAnimationKey = animKey;
                _currentAnimation = newAnimation;
                _currentFrameIndex = 0;
                _frameTimer = 0;
            }
            // If the key is not found, the animation simply doesn't change.
        }
        public Rectangle GetFrame(string bodyPartName)
        {
            if (_currentAnimation == null || !_currentAnimation.Frames[_currentFrameIndex].Parts.TryGetValue(bodyPartName, out var framePos))
            {
                return Rectangle.Empty; // Return an empty rectangle if the part isn't in this frame
            }

            return new Rectangle(
                framePos.X * _animationFile.FrameWidth,
                framePos.Y * _animationFile.FrameHeight,
                _animationFile.FrameWidth,
                _animationFile.FrameHeight
            );
        }
        public int GetCurrentAnimationFramerate()
        {
            return _currentAnimation?.Frames.Count ?? 0;
        }
        public int GetDrawOrder(string bodyPartName, Direction direction, bool Turning)
        {
            // The JSON uses "East" for both East and West draw orders
            string dirKey = (direction == Direction.West) ? "East" : direction.ToString();

            if (_animationFile.DrawOrder.TryGetValue(dirKey, out var order) && order.TryGetValue(bodyPartName, out int value))
            {
                return value;
            }
            return 99; // Draw on top if not specified
        }
    }
}