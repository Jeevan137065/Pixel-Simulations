using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    public enum Direction { South, North, East, West }
    public enum PlayerState { Idle, Walking, Turning }
    public abstract class Human
    {
        // Core Properties
        public string Name { get; protected set; }
        public Vector2 Position { get; protected set; }
        public Vector2 Velocity { get; protected set; }
        public RectangleF BoundingBox { get; protected set; }
        public Direction FacingDirection { get; protected set; }

    }
    public class NewPlayer : Human
    {
        // --- State Properties ---
        public PlayerState CurrentState { get; private set; }

        // --- Animation Management ---
        private AnimationManager _animationManager;
        private List<BodyPart> _bodyParts;
        //private PhysicsManager _physics;
        // --- Movement & Input ---
        private float _speed = 100f;
        private bool _isTurning = false;
        private float _turnTimer = 0f;

        private const float TURN_FRAME_DURATION = 0.6f; // How fast each frame of the turn animation plays
        private int _turnFrameCount = 3; // Number of frames in your South -> East turn animation

        private Direction _targetDirection;
        private KeyboardState _previousKeyboardState;

        private GraphicsDevice _graphicsDevice;
        private RenderTarget2D _compositeTarget;
        private SpriteBatch _localSpriteBatch;
        public Texture2D CompositeTexture => _compositeTarget;
        public NewPlayer(string name, Vector2 startPosition, GraphicsDevice graphicsDevice)
        {
            Name = name;
            Position = startPosition;
            _graphicsDevice = graphicsDevice;
            _localSpriteBatch = new SpriteBatch(_graphicsDevice);
            _compositeTarget = new RenderTarget2D(_graphicsDevice,48, 64,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);

            FacingDirection = Direction.South;
            CurrentState = PlayerState.Idle;
            _previousKeyboardState = Keyboard.GetState();
            _animationManager = new AnimationManager();
            _bodyParts = new List<BodyPart>();
        }

        public  void LoadContent(ContentManager content,PhysicsManager physics)
        {
            // The AnimationManager will handle loading the texture and JSON
            _animationManager.LoadContent(content,"BodySheet", "player_animation_data.json");

            // Create the body part instances
            _bodyParts.Add(new BodyPart("Torso"));
            _bodyParts.Add(new BodyPart("LeftArm"));
            _bodyParts.Add(new BodyPart("RightArm"));
            _bodyParts.Add(new BodyPart("LeftLeg"));
            _bodyParts.Add(new BodyPart("RightLeg"));

            //_physics = physics;
        }

        public void Update(GameTime gameTime)
        {
            if (_isTurning)
            {
                UpdateTurning(gameTime);
            }
            else
            {
                HandleInput(gameTime);
            }

            // Update all body parts with their new frames from the animation manager
            _animationManager.Update(gameTime, CurrentState, FacingDirection, FacingDirection, _isTurning);
            foreach (var part in _bodyParts)
            {
                part.SourceRectangle = _animationManager.GetFrame(part.Name);
            }

            // Update the drawing order based on the current state and direction
            UpdateDrawOrder();

            ComposeTexture();
        }

        private void HandleInput(GameTime gameTime)
        {
            var kbs = Keyboard.GetState();
            Vector2 moveDirection = Vector2.Zero;

            if (kbs.IsKeyDown(Keys.W)) moveDirection.Y -= 1;
            if (kbs.IsKeyDown(Keys.S)) moveDirection.Y += 1;
            if (kbs.IsKeyDown(Keys.A)) moveDirection.X -= 1;
            if (kbs.IsKeyDown(Keys.D)) moveDirection.X += 1;

            if (moveDirection != Vector2.Zero)
            {
                CurrentState = PlayerState.Walking;
                Vector2 desiredVelocity = Vector2.Normalize(moveDirection) * _speed * (float)gameTime.ElapsedGameTime.TotalSeconds;

                // *** THE PHYSICS CHECK ***
                // Define the player's collision box (usually smaller than the full sprite, near the feet)
                RectangleF playerCollisionBox = new RectangleF(Position.X + 8, Position.Y + 32, 16, 16);

                // Ask the physics manager how much of that desired velocity is allowed
                //Vector2 allowedVelocity = _physics.ResolveMovement(playerCollisionBox, desiredVelocity);
                Vector2 allowedVelocity = desiredVelocity;

                Position += allowedVelocity;
            }
            else
            {
                CurrentState = PlayerState.Idle;
                Velocity = Vector2.Zero;
            }

            // --- 3. DETERMINE FACING DIRECTION (THE FIX FOR BUGS A, B, and C) ---
            // Prioritize horizontal facing direction if there is any horizontal movement.
            if (moveDirection.X > 0)
            {
                FacingDirection = Direction.East;
            }
            else if (moveDirection.X < 0)
            {
                FacingDirection = Direction.West;
            }
            // Only face North or South if there is NO horizontal movement.
            else if (moveDirection.Y > 0)
            {
                FacingDirection = Direction.South;
            }
            else if (moveDirection.Y < 0)
            {
                FacingDirection = Direction.North;
            }
            // If there is no movement at all, the FacingDirection remains what it was.

            // --- 4. HANDLE SINGLE-PRESS TURNING ---
            // This allows turning without moving. Overrides the movement-based facing direction.
            if (moveDirection == Vector2.Zero)
            {
                if (kbs.IsKeyDown(Keys.D) && _previousKeyboardState.IsKeyUp(Keys.D)) StartTurn(Direction.East);
                else if (kbs.IsKeyDown(Keys.A) && _previousKeyboardState.IsKeyUp(Keys.A)) StartTurn(Direction.West);
                else if (kbs.IsKeyDown(Keys.W) && _previousKeyboardState.IsKeyUp(Keys.W)) StartTurn(Direction.North);
                else if (kbs.IsKeyDown(Keys.S) && _previousKeyboardState.IsKeyUp(Keys.S)) StartTurn(Direction.South);
            }

            _previousKeyboardState = kbs; // Update for the next frame
        }

        private void StartTurn(Direction targetDirection)
        {
            // Don't start a new turn if we're already turning or already facing the target direction
            if (_isTurning || FacingDirection == targetDirection) return;

            _isTurning = true;
            _turnTimer = 0f;
            CurrentState = PlayerState.Turning;
            _targetDirection = targetDirection;

            // Determine the animation key for the turn
            string turnKey = $"Turn_{FacingDirection}_{_targetDirection}";
            _animationManager.SetCurrentAnimation(turnKey); // We need this new method in AnimationManager

            // Get the frame count for this specific turn animation
            _turnFrameCount = _animationManager.GetCurrentAnimationFramerate();
        }

        private void UpdateTurning(GameTime gameTime)
        {
            _turnTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Check if the entire turn animation sequence is complete
            if (_turnTimer >= (_turnFrameCount * TURN_FRAME_DURATION))
            {
                _isTurning = false;
                FacingDirection = _targetDirection; // Finalize the direction
                CurrentState = PlayerState.Idle; // Return to Idle state
            }
        }

        private void UpdateDrawOrder()
        {
            _bodyParts.Sort((a, b) =>
                _animationManager.GetDrawOrder(a.Name, FacingDirection, _isTurning)
                .CompareTo(_animationManager.GetDrawOrder(b.Name, FacingDirection, _isTurning))
            );
        }

        private void ComposeTexture()
        {
            // 1. Store the current render target so we can restore it later
            RenderTargetBinding[] currentTargets = _graphicsDevice.GetRenderTargets();

            // 2. Set our internal target as the active canvas
            _graphicsDevice.SetRenderTarget(_compositeTarget);

            // 3. Clear it to fully transparent
            _graphicsDevice.Clear(Color.Transparent);

            // 4. Begin the local SpriteBatch. PointClamp ensures crisp pixels.
            _localSpriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Determine mirroring based on direction
            SpriteEffects effect = (FacingDirection == Direction.West) ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // Draw all parts relative to the center of the 48x64 render target
            Vector2 centerOrigin = new Vector2(24, 32);

            // The parts in the spritesheet have their own origin. 
            // Usually, the origin is the center of the frame (16, 24 for a 32x48 frame).
            Vector2 partOrigin = new Vector2(24, 32);

            foreach (var part in _bodyParts)
            {
                if (part.SourceRectangle != Rectangle.Empty)
                {
                    _localSpriteBatch.Draw(
                        _animationManager.SpriteSheet,
                        centerOrigin,        // Draw at the center of the RenderTarget
                        part.SourceRectangle,
                        Color.White,
                        0f,
                        partOrigin,          // The origin point within the source frame
                        1f,
                        effect,
                        0f
                    );
                }
            }

            _localSpriteBatch.End();

            // 5. Restore the original render targets
            _graphicsDevice.SetRenderTargets(currentTargets);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            Vector2 drawOrigin = new Vector2(24, 32);

            // Depth is based on the player's feet (Position.Y + height)
            float bottomY = Position.Y + 48; // Assuming the player sprite is 48px tall
            float depth = DepthUtil.Calculate(bottomY);

            spriteBatch.Draw(_compositeTarget, Position, null, Color.White, 0f, drawOrigin, 1f, SpriteEffects.None, depth);
        }
    }

    // A simple class to hold the state of a single body part
    public class BodyPart
    {
        public string Name { get; }
        public Rectangle SourceRectangle { get; set; }

        public BodyPart(string name)
        {
            Name = name;
        }
    }
}
