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
    public enum Tool { None, Shovel, Hoe, LeekSeeds, CarrotSeeds, TurnipSeeds, DiakonSeeds }
    public abstract class Human
    {
        // Core Properties
        public string Name { get; protected set; }
        public Vector2 Position { get; protected set; }
        public Vector2 Velocity { get; protected set; }
        public Direction FacingDirection { get; protected set; }

    }
    public class NewPlayer : Human
    {
        // --- State Properties ---
        public PlayerState CurrentState { get; private set; }
        public Inventory Inventory { get; private set; }
        public bool IsMoving => Velocity != Vector2.Zero;

        // --- Visual & Shader Properties for GameRenderer ---
        public Texture2D Texture => _compositeTarget;
        public Rectangle SourceRect => new Rectangle(0, 0, 48, 64);
        public Vector2 Origin => new Vector2(24, 48); // Local pivot inside the 48x64 texture
        public Vector2 Foot => Position; // World position of the feet

        // FootBounds represents the physical space the player occupies on the ground.
        // Used for Physics, Grass flattening (Green Channel), and Interaction.
        public RectangleF FootBounds => new RectangleF(Position.X - 8, Position.Y - 4, 16, 8);

        // --- Internal Components ---
        private AnimationManager _animationManager;
        private List<BodyPart> _bodyParts;
        private PhysicsManager _physics;

        private float _speed = 100f;
        private bool _isTurning = false;
        private float _turnTimer = 0f;
        private const float TURN_FRAME_DURATION = 0.6f;
        private int _turnFrameCount = 3;

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
            Inventory = new Inventory();

            _graphicsDevice = graphicsDevice;
            _localSpriteBatch = new SpriteBatch(_graphicsDevice);
            _compositeTarget = new RenderTarget2D(_graphicsDevice, 48, 64, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

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

            _physics = physics;
        }

        public void Update(GameTime gameTime)
        {
            if (_isTurning) UpdateTurning(gameTime);
            else HandleInput(gameTime);

            _animationManager.Update(gameTime, CurrentState, FacingDirection, FacingDirection, _isTurning);
            foreach (var part in _bodyParts)
            {
                part.SourceRectangle = _animationManager.GetFrame(part.Name);
            }
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

            // Handle Hotbar
            if (kbs.IsKeyDown(Keys.D1)) Inventory.SelectSlot(0);
            if (kbs.IsKeyDown(Keys.D2)) Inventory.SelectSlot(1);
            if (kbs.IsKeyDown(Keys.D3)) Inventory.SelectSlot(2);
            if (kbs.IsKeyDown(Keys.D4)) Inventory.SelectSlot(3);
            if (kbs.IsKeyDown(Keys.D5)) Inventory.SelectSlot(4);
            if (kbs.IsKeyDown(Keys.D6)) Inventory.SelectSlot(5);

            if (moveDirection != Vector2.Zero)
            {
                CurrentState = PlayerState.Walking;
                Vector2 desiredVelocity = Vector2.Normalize(moveDirection) * _speed * (float)gameTime.ElapsedGameTime.TotalSeconds;

                // Player collision box (feet)
                RectangleF playerCollisionBox = new RectangleF(Position.X - 8, Position.Y + 16, 16, 16);

                // Resolve movement through PhysicsManager
                Vector2 allowedVelocity = _physics.ResolveMovement(playerCollisionBox, desiredVelocity);
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
        public RectangleF GetInteractionBox()
        {
            // True center of the player's feet
            float centerX = Position.X;
            float feetY = Position.Y + 24;

            float w = 16;
            float h = 16;
            float reach = 20f;

            return FacingDirection switch
            {
                // Reach UP from feet
                Direction.North => new RectangleF(centerX - w / 2, feetY - h - reach, w, reach),
                // Reach DOWN from feet
                Direction.South => new RectangleF(centerX - w / 2, feetY, w, reach),
                // Reach LEFT from center
                Direction.West => new RectangleF(centerX - w / 2 - reach, feetY - h, reach, h),
                // Reach RIGHT from center
                Direction.East => new RectangleF(centerX + w / 2, feetY - h, reach, h),
                _ => new RectangleF(centerX - w / 2, feetY - h, w, h)
            };
        }

        // The source rect is the entire composite target
        public Rectangle CurrentFrameRect => new Rectangle(0, 0, _compositeTarget.Width, _compositeTarget.Height);


        // The exact Y coordinate of the player's feet touching the ground
        public float BaseY => Position.Y + 48f;

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
    public class Inventory
    {
        public const int HotbarSize = 10;
        public Tool[] Hotbar { get; } = new Tool[HotbarSize];
        public int SelectedSlot { get; private set; }

        public Inventory()
        {
            Hotbar[0] = Tool.Shovel; Hotbar[1] = Tool.Hoe; Hotbar[2] = Tool.LeekSeeds;
            Hotbar[3] = Tool.CarrotSeeds; Hotbar[4] = Tool.TurnipSeeds; Hotbar[5] = Tool.DiakonSeeds;
        }

        public void SelectSlot(int slot) { if (slot >= 0 && slot < HotbarSize) SelectedSlot = slot; }
        public Tool GetSelectedItem() => Hotbar[SelectedSlot];
        public bool AddItem(Tool itemToAdd)
        {
            for (int i = 2; i < HotbarSize; i++) { if (Hotbar[i] == Tool.None) { Hotbar[i] = itemToAdd; return true; } }
            return false;
        }
    }
}
