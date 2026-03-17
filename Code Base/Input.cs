using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using Pixel_Simulations.Data;
using System.Collections.Generic;

namespace Pixel_Simulations
{
    public class EditorInputState
    {
        public MouseState CurrentMouse { get; set; }
        public MouseState PreviousMouse { get; set; }
        public KeyboardState CurrentKeyboard { get; set; }
        public KeyboardState PreviousKeyboard { get; set; }
        public bool Drawing = false;
        public int ClickCounter = 0;
        // Click Flags (True for one frame)
        public bool IsNewLeftClick { get; private set; }
        public bool IsNewRightClick { get; private set; }
        public bool NewDoubleLeftClick { get; private set; }
        public bool NewDoubleRightClick { get; private set; }

        // Hold Flags (True while button is down)
        public bool LeftHold => CurrentMouse.LeftButton == ButtonState.Pressed;
        public bool RightHold => CurrentMouse.RightButton == ButtonState.Pressed;

        private float _lastLeftClickTime = -1f;
        private float _lastRightClickTime = -1f;
        private const float DOUBLE_CLICK_THRESHOLD = 0.3f; // Seconds

        public Vector2 MouseWindowPosition { get; set; }
        public Vector2 MouseWorldPosition { get; set; }
        public Point MouseGridCell { get; set; }
        public Point MouseChunkCell { get; set; }
        public float Zoom;
        public void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.TotalGameTime.TotalSeconds;

            // Reset frame-specific flags
            IsNewLeftClick = false;
            IsNewRightClick = false;
            NewDoubleLeftClick = false;
            NewDoubleRightClick = false;

            // --- Left Click Logic ---
            if (CurrentMouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released)
            {
                if (elapsed - _lastLeftClickTime < DOUBLE_CLICK_THRESHOLD)
                {
                    NewDoubleLeftClick = true;
                    _lastLeftClickTime = -1f;
                    ClickCounter++;
                }
                else
                {
                    IsNewLeftClick = true;
                    _lastLeftClickTime = elapsed;
                }
                ClickCounter++;
            }

            // --- Right Click Logic ---
            if (CurrentMouse.RightButton == ButtonState.Pressed && PreviousMouse.RightButton == ButtonState.Released)
            {
                if (elapsed - _lastRightClickTime < DOUBLE_CLICK_THRESHOLD)
                {
                    NewDoubleRightClick = true;
                    _lastRightClickTime = -1f;
                    ClickCounter++;
                }
                else
                {
                    IsNewRightClick = true;
                    _lastRightClickTime = elapsed;
                }
                ClickCounter++;
            }
        }
    }
    public class InputManager
    {
        public MouseState CurrentMouse { get; private set; }
        public MouseState PreviousMouse { get; private set; }
        public KeyboardState CurrentKeyboard { get; private set; }
        public KeyboardState PreviousKeyboard { get; private set; }

        // --- Mouse Helpers ---
        public Vector2 MouseScreenPosition => CurrentMouse.Position.ToVector2();
        public Vector2 MouseWorldPosition { get; private set; }

        public bool LeftDown => CurrentMouse.LeftButton == ButtonState.Pressed;
        public bool RightDown => CurrentMouse.RightButton == ButtonState.Pressed;
        public bool NewLeftClick => CurrentMouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released;
        public bool NewRightClick => CurrentMouse.RightButton == ButtonState.Pressed && PreviousMouse.RightButton == ButtonState.Released;

        // --- Double Click Logic ---
        private float _leftClickTimer = 0f;
        private const float DOUBLE_CLICK_THRESHOLD = 0.3f;
        public bool NewLeftDoubleClick { get; private set; }

        public void Update(GameTime gameTime, Camera camera)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update States
            PreviousMouse = CurrentMouse;
            PreviousKeyboard = CurrentKeyboard;
            CurrentMouse = Mouse.GetState();
            CurrentKeyboard = Keyboard.GetState();

            // Calculate World Position (Transform screen mouse by camera inverse)
            // Note: Use NativeView because it represents the 480x270 coordinates
            Matrix invView = Matrix.Invert(camera.NativeView);
            MouseWorldPosition = Vector2.Transform(MouseScreenPosition, invView);

            // Double Click Detection
            NewLeftDoubleClick = false;
            if (NewLeftClick)
            {
                if (_leftClickTimer > 0)
                {
                    NewLeftDoubleClick = true;
                    _leftClickTimer = 0;
                }
                else
                {
                    _leftClickTimer = DOUBLE_CLICK_THRESHOLD;
                }
            }

            if (_leftClickTimer > 0) _leftClickTimer -= dt;
        }

        // --- Keyboard Helpers ---
        public bool IsKeyDown(Keys key) => CurrentKeyboard.IsKeyDown(key);

        /// <summary> True only on the frame the key was first pressed. </summary>
        public bool IsKeyPressed(Keys key) => CurrentKeyboard.IsKeyDown(key) && PreviousKeyboard.IsKeyUp(key);

        public int GetScrollDelta() => CurrentMouse.ScrollWheelValue - PreviousMouse.ScrollWheelValue;
    }
}