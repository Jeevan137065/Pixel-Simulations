using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace Pixel_Simulations
{ 
    public enum RenderLayer
    {
        Albedo,     // Low-Res: Static ground colors
        Normal,     // Low-Res: For lighting (Future)
        Dynamic,    // High-Res: Swaying crops, player
        Depth,      // High-Res: Z-buffer visualization
        LightMask,  // High-Res: Lighting overlay
        Composite   // High-Res: Final combined image
    }
    public class Upscaler
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _renderTarget; // The primary, internal canvas
        public RenderTarget2D _motionTarget;
        private RenderTarget2D _postProcessTarget;
        public readonly Rectangle _destinationRect;
        public int NativeWidth { get; }
        public int NativeHeight { get; }
        public int Scale { get; }

        public RenderTarget2D RenderTarget => _renderTarget;
        public Upscaler(GraphicsDevice graphicsDevice, int windowWidth, int windowHeight, int scale)
        {
            _graphicsDevice = graphicsDevice;
            NativeWidth = windowWidth;
            NativeHeight = windowHeight;
            Scale = scale;

            // Create the main render target for the upscaler's final low-res image
            _renderTarget = new RenderTarget2D(graphicsDevice, windowWidth, windowHeight);

            // Create the intermediate target, same size
            _postProcessTarget = new RenderTarget2D(graphicsDevice, windowWidth, windowHeight);

            int finalWidth = windowWidth * scale;
            int finalHeight = windowHeight * scale;
            _motionTarget = new RenderTarget2D(graphicsDevice, finalWidth, finalHeight);
            _destinationRect = new Rectangle(0, 0, finalWidth, finalHeight);
        }


        public void SetWindowResolution(GraphicsDeviceManager graphicsManager)
        {
            graphicsManager.PreferredBackBufferWidth = _destinationRect.Width;
            graphicsManager.PreferredBackBufferHeight = _destinationRect.Height;
            graphicsManager.IsFullScreen = true;
            graphicsManager.ApplyChanges();
        }
        //NOTE: We will NOT use SetWindowResolution for the editor, as it's for fullscreen games.
        public void BeginMotionRender()
        {
            _graphicsDevice.SetRenderTarget(_motionTarget);
            // Clear with transparent so we can see the static world behind it.
            _graphicsDevice.Clear(Color.Transparent);
        }

        public void ContinueMotionRender()
        {
            _graphicsDevice.SetRenderTarget(_motionTarget);
            // Clear with transparent so we can see the static world behind it.
            //_graphicsDevice.Clear(Color.Transparent);
        }

        public void BeginRender()
        {
            _graphicsDevice.SetRenderTarget(_renderTarget);
            _graphicsDevice.Clear(Color.Transparent);
        }

        public void Present( SpriteBatch spriteBatch)
        {
            _graphicsDevice.SetRenderTarget(null);
            _graphicsDevice.Clear(Color.Black);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
            spriteBatch.Draw(_renderTarget, _destinationRect, Color.White);
            spriteBatch.End();
        }


        public void Present(SpriteBatch spriteBatch, RenderTarget2D sourceTexture, params Effect[] effects)
        {
            RenderTarget2D currentSource = sourceTexture;

            if (effects != null && effects.Length > 0)
            {
                // We will ping-pong between our two internal render targets.
                RenderTarget2D[] targets = { _postProcessTarget, _renderTarget };

                for (int i = 0; i < effects.Length; i++)
                {
                    Effect effect = effects[i];
                    if (effect == null) continue; // Skip null effects

                    // Determine the destination for this pass
                    RenderTarget2D destination = targets[i % 2];

                    _graphicsDevice.SetRenderTarget(destination);
                    _graphicsDevice.Clear(Color.Transparent);

                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, null, null, effect);
                    spriteBatch.Draw(currentSource, Vector2.Zero, Color.White);
                    spriteBatch.End();

                    // The output of this pass is the input for the next
                    currentSource = destination;
                }
            }

            // --- Final Draw to the Back Buffer ---
            _graphicsDevice.SetRenderTarget(null);
            _graphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
            // Draw the final result (which is now in 'currentSource') to the screen
            spriteBatch.Draw(currentSource, _destinationRect, Color.White);
            spriteBatch.End();
        }

        public void Present(SpriteBatch spriteBatch, RenderTarget2D staticTarget)
        {
            // Set the final render target to the screen (back buffer)
            _graphicsDevice.SetRenderTarget(null);
            _graphicsDevice.Clear(Color.Black);

            // --- BATCH 1: Draw the upscaled static background ---
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
            spriteBatch.Draw(staticTarget, _destinationRect, Color.White);
            spriteBatch.End();

            // --- BATCH 2: Draw the high-resolution motion layer on top ---
            // Use AlphaBlend so the transparent parts of motionTarget don't draw anything.
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(_motionTarget, _destinationRect, Color.White);
            spriteBatch.End();
        }

    }

    public class EditorUpscaler
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _nativeRenderTarget; // The low-res 480x270 canvas

        public int NativeWidth { get; }
        public int NativeHeight { get; }
        public int Scale { get; }

        public EditorUpscaler(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            NativeWidth = 480;
            NativeHeight = 270;
            Scale = 2; // Upscale 2x for the editor view

            // Create the render target that our game world will be drawn to
            _nativeRenderTarget = new RenderTarget2D(
                _graphicsDevice,
                NativeWidth,
                NativeHeight,
                false,
                _graphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24
            );
        }

        /// <summary>
        /// Sets the GraphicsDevice to render to the internal low-resolution target.
        /// Call this before drawing the world.
        /// </summary>
        public void BeginRender()
        {
            _graphicsDevice.SetRenderTarget(_nativeRenderTarget);
        }

        /// <summary>
        /// Draws the upscaled world content to the screen within the specified viewport area.
        /// Call this during the final UI composition pass.
        /// </summary>
        public void Present(SpriteBatch spriteBatch, Rectangle viewportDestination)
        {
            // First, ensure we are drawing back to the main screen
            _graphicsDevice.SetRenderTarget(null);

            // Draw the contents of our low-res render target, scaled up to fit the viewport area.
            // SamplerState.PointClamp is crucial to maintain the crisp pixel-art look.
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_nativeRenderTarget, viewportDestination, Color.White);
            spriteBatch.End();
        }
    }

    public class RenderPipeline
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly int _nativeWidth;
        private readonly int _nativeHeight;
        private readonly int _scale;

        // Dictionary to store our RenderTargets by Enum
        private readonly Dictionary<RenderLayer, RenderTarget2D> _targets;

        // Rectangles for drawing
        public Rectangle NativeRect { get; }
        public Rectangle HighResRect { get; }

        private RenderLayer _currentDebugView = RenderLayer.Composite;
        private KeyboardState _prevKeyboardState;
        private readonly List<RenderLayer> _debugCycleOrder;
        public float Scale => _scale;
        public bool IsDebugDepthActive => _currentDebugView == RenderLayer.Depth;
        public bool IsNormalPassActive => _currentDebugView == RenderLayer.Normal;
        public string CurrentViewName => _currentDebugView.ToString();

        public RenderPipeline(GraphicsDevice graphicsDevice, int nativeWidth, int nativeHeight, int scale)
        {
            _graphicsDevice = graphicsDevice;
            _nativeWidth = nativeWidth;
            _nativeHeight = nativeHeight;
            _scale = scale;

            NativeRect = new Rectangle(0, 0, nativeWidth, nativeHeight);
            HighResRect = new Rectangle(0, 0, nativeWidth * scale, nativeHeight * scale);

            _targets = new Dictionary<RenderLayer, RenderTarget2D>();
            _debugCycleOrder = new List<RenderLayer>
        {
            RenderLayer.Composite, // Final Game
            RenderLayer.Albedo,    // Static Background
            RenderLayer.Dynamic,   // Moving Objects
            RenderLayer.Normal,
            RenderLayer.Depth      // Depth Visualization
        };
            InitializeTargets();
        }

        public void SetWindowResolution(GraphicsDeviceManager graphicsManager)
        {
            graphicsManager.PreferredBackBufferWidth = HighResRect.Width;
            graphicsManager.PreferredBackBufferHeight = HighResRect.Height;
            graphicsManager.IsFullScreen = true;
            graphicsManager.ApplyChanges();
        }

        private void InitializeTargets()
        {
            // 1. LOW RES TARGETS (Static World)
            // We don't need depth buffers for the flat background
            _targets[RenderLayer.Albedo] = new RenderTarget2D(_graphicsDevice, NativeRect.Width, NativeRect.Height);
            _targets[RenderLayer.Normal] = new RenderTarget2D(_graphicsDevice, HighResRect.Width, HighResRect.Height);

            // 2. HIGH RES TARGETS (Dynamic World)
            // IMPORTANT: Dynamic layer needs DepthFormat.Depth24 for proper Z-Sorting of crops/player!
            _targets[RenderLayer.Dynamic] = new RenderTarget2D(_graphicsDevice, HighResRect.Width, HighResRect.Height, false, SurfaceFormat.Color, DepthFormat.Depth24);

            // Visualization targets
            _targets[RenderLayer.Depth] = new RenderTarget2D(_graphicsDevice, HighResRect.Width, HighResRect.Height, false, SurfaceFormat.Color, DepthFormat.Depth24);
            _targets[RenderLayer.LightMask] = new RenderTarget2D(_graphicsDevice, HighResRect.Width, HighResRect.Height);

            // Final Result
            _targets[RenderLayer.Composite] = new RenderTarget2D(_graphicsDevice, HighResRect.Width, HighResRect.Height);
        }

        public void Update()
        {
            var kstate = Keyboard.GetState();

            if (kstate.IsKeyDown(Keys.P) && _prevKeyboardState.IsKeyUp(Keys.P))
            {
                // Cycle to next view
                int currentIndex = _debugCycleOrder.IndexOf(_currentDebugView);
                int nextIndex = (currentIndex + 1) % _debugCycleOrder.Count;
                _currentDebugView = _debugCycleOrder[nextIndex];
            }
            _prevKeyboardState = kstate;
        }

        /// Sets the target and CLEARS it.
        public void Begin(RenderLayer layer, Color clearColor)
        {
            _graphicsDevice.SetRenderTarget(_targets[layer]);
            _graphicsDevice.Clear(clearColor);
        }
        /// Sets the target WITHOUT clearing. Useful for multi-pass rendering to the same layer.
        public void Continue(RenderLayer layer)
        {
            _graphicsDevice.SetRenderTarget(_targets[layer]);
            // No clear
        }

        /// Get a reference to a target texture (for shaders or debugging)
        public Texture2D GetTarget(RenderLayer layer)
        {
            return _targets[layer];
        }

        /// Helper to render a specific layer to the screen (or another target) with an effect.
        public void PresentLayer(SpriteBatch spriteBatch, RenderLayer layer, Effect effect = null)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, effect);
            spriteBatch.Draw(_targets[layer], HighResRect, Color.White);
            spriteBatch.End();
        }

        /// The final logic to combine everything and draw to the BackBuffer.
        public void PresentFinal(SpriteBatch spriteBatch)
        {
            // 1. COMPOSE STEP: Combine everything into the Composite Target
            _graphicsDevice.SetRenderTarget(_targets[RenderLayer.Composite]);
            _graphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            // A. Draw Albedo (Scaled up to fill background)
            spriteBatch.Draw(_targets[RenderLayer.Albedo], HighResRect, Color.White);

            // B. Draw Dynamic (Already High-Res, drawn on top)
            spriteBatch.Draw(_targets[RenderLayer.Dynamic], HighResRect, Color.White);

            // C. (Future) Draw LightMask with Multiply blend mode...

            spriteBatch.End();

            // 2. SCREEN STEP: Draw Composite to the actual screen
            _graphicsDevice.SetRenderTarget(null);
            _graphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
            spriteBatch.Draw(_targets[_currentDebugView], HighResRect, Color.White);
            spriteBatch.End();
        }
    }
}