using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace Pixel_Simulations
{
    public enum RenderLayer
    {
        Albedo,         // 480x270: Static background
        DepthOnly,      // 960p: Procedural depth/stencil pre-pass (Float)
        Dynamic,        // 960x540: Grass, Player, NPCs (The G-Buffer Color)
        Normal,         // 960x540: Normal maps for lighting
        LightMask,      // 960x540: HDR Lighting calculation
        Composite       // Final: Combined result at Window Resolution
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
        private readonly int _simScale = 2; // 480 -> 960

        private readonly Dictionary<RenderLayer, RenderTarget2D> _targets;

        public Rectangle NativeRect { get; }      // 480x270
        public Rectangle SimRect { get; }         // 960x540
        public Rectangle FinalRect { get; }       // e.g. 1920x1080

        public RenderLayer _currentDebugView = RenderLayer.Composite;
        private KeyboardState _prevKeyboardState;
        private readonly List<RenderLayer> _debugCycleOrder;
        public string currentRT = null;

        public RenderPipeline(GraphicsDevice graphicsDevice, int nativeWidth, int nativeHeight, int finalWidth, int finalHeight)
        {
            _graphicsDevice = graphicsDevice;
            _nativeWidth = nativeWidth;
            _nativeHeight = nativeHeight;

            NativeRect = new Rectangle(0, 0, nativeWidth, nativeHeight);
            SimRect = new Rectangle(0, 0, nativeWidth * _simScale, nativeHeight * _simScale);
            FinalRect = new Rectangle(0, 0, finalWidth, finalHeight);

            _targets = new Dictionary<RenderLayer, RenderTarget2D>();
            _debugCycleOrder = new List<RenderLayer>
            {
                RenderLayer.Composite,
                RenderLayer.Albedo,
                RenderLayer.DepthOnly,
                RenderLayer.Dynamic,
                RenderLayer.Normal,
                RenderLayer.LightMask
            };

            InitializeTargets();
        }

        public void SetWindowResolution(GraphicsDeviceManager graphicsManager)
        {
            graphicsManager.PreferredBackBufferWidth = FinalRect.Width;
            graphicsManager.PreferredBackBufferHeight = FinalRect.Height;
            graphicsManager.IsFullScreen = true;
            graphicsManager.ApplyChanges();
        }

        private void InitializeTargets()
        {
            // 1. NATIVE LAYER (480x270)
            _targets[RenderLayer.Albedo] = new RenderTarget2D(_graphicsDevice, NativeRect.Width, NativeRect.Height);

            // 2. SIMULATION LAYER (960x540)
            // This is our Master Depth/Stencil target. All simulation draws share this buffer.
            _targets[RenderLayer.DepthOnly] = new RenderTarget2D(_graphicsDevice, SimRect.Width, SimRect.Height, false,
                SurfaceFormat.HalfVector2, DepthFormat.Depth24Stencil8);

            _targets[RenderLayer.Dynamic] = new RenderTarget2D(_graphicsDevice, SimRect.Width, SimRect.Height, false,
                SurfaceFormat.Color, DepthFormat.Depth24Stencil8); // Shared DepthStencil memory
            // Normals: Used for deferred lighting
            _targets[RenderLayer.Normal] = new RenderTarget2D(_graphicsDevice, SimRect.Width, SimRect.Height);

            // LightMask: Using HalfVector4 for HDR (Floating point lighting values)
            _targets[RenderLayer.LightMask] = new RenderTarget2D(_graphicsDevice, SimRect.Width, SimRect.Height, false,
                SurfaceFormat.HalfVector4, DepthFormat.None);

            // 3. FINAL LAYER
            _targets[RenderLayer.Composite] = new RenderTarget2D(_graphicsDevice, FinalRect.Width, FinalRect.Height);
        }

        public void BeginDepthPass()
        {
            _graphicsDevice.SetRenderTarget(_targets[RenderLayer.DepthOnly]);
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1.0f, 0);
        }

        // --- PHASE B: DYNAMIC COLOR PASS ---
        // We use the depth/stencil from the previous pass to only draw pixels that passed the test.
        public void BeginDynamicPass()
        {
            _graphicsDevice.SetRenderTarget(_targets[RenderLayer.Dynamic]);
            // We do NOT clear depth here! We want to "Continue" using the depth from Phase A.
            _graphicsDevice.Clear(Color.Transparent);
        }

        // --- PHASE C: NORMAL PASS ---
        // Exclusive pass for Normal data.
        public void BeginNormalPass()
        {
            _graphicsDevice.SetRenderTarget(_targets[RenderLayer.Normal]);
            _graphicsDevice.Clear(Color.Transparent);
        }

        public void Begin(RenderLayer layer, Color clearColor)
        {
            _graphicsDevice.SetRenderTarget(_targets[layer]);

            if (_targets[layer].DepthStencilFormat != DepthFormat.None)
                _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, clearColor, 1.0f, 0);
            else
                _graphicsDevice.Clear(clearColor);
        }

        public RenderTargetBinding GetTarget(RenderLayer layer) => _targets[layer];

        public void Update()
        {
            var kstate = Keyboard.GetState();
            if (kstate.IsKeyDown(Keys.P) && _prevKeyboardState.IsKeyUp(Keys.P))
            {
                int currentIndex = _debugCycleOrder.IndexOf(_currentDebugView);
                _currentDebugView = _debugCycleOrder[(currentIndex + 1) % _debugCycleOrder.Count];
                currentRT = _targets[_currentDebugView].ToString();
            }
            _prevKeyboardState = kstate;
        }

        /// <summary>
        /// The Composite Engine. Combines 480p and 960p layers into the final user resolution.
        /// </summary>
        public void PresentFinal(SpriteBatch spriteBatch, Effect compositeEffect = null)
        {
            // 1. COMPOSE TO INTERNAL BUFFER
            _graphicsDevice.SetRenderTarget(_targets[RenderLayer.Composite]);
            _graphicsDevice.Clear(Color.Black);

            // If we have a composite effect (for lighting/HDR), we use it here
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, compositeEffect);

            // A. Draw Albedo (480p Background) scaled to fit the 1080p+ FinalRect
            spriteBatch.Draw(_targets[RenderLayer.Albedo], FinalRect, Color.White);

            // B. Draw Simulation Layer (960p Dynamic) scaled to fit FinalRect
            spriteBatch.Draw(_targets[RenderLayer.Dynamic], FinalRect, Color.White);

            spriteBatch.End();

            // 2. OUTPUT TO SCREEN
            _graphicsDevice.SetRenderTarget(null);
            _graphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
            // Draw whichever layer is currently selected for debugging
            spriteBatch.Draw(_targets[_currentDebugView], FinalRect, Color.White);
            spriteBatch.End();
        }
    }
}