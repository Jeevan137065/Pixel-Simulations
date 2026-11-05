using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Pixel_Simulations.Editor;
using System.Collections.Generic;

namespace Pixel_Simulations
{
    public enum RenderLayer
    {
        Albedo,         // 480x270: Static background
        YDepth,         // 960x540: R: Volume Depth (Altitude), G: Light/Shadow Vol, B: Surface Depth
        ZDepth,         // 960x540: R: Z-Depth (Sorting), G: Light Depth (3D shadow), B: Shading/Material
        WaterData,      // 960x540 [HalfVector4] R: SDF Shore, G: Flow X, B: Flow Y, A: Dynamic Wave/Ripples
        Dynamic,        // 960x540: Grass, Player, NPCs (G-Buffer Color)
        Simulation,     // 480p: For physical items
        Normal,         // 960x540: Normal maps for lighting
        LightMask,      // 960x540: HDR Lighting calculation
        Shader,         // 960p: Weather and atmospheric shader
        Composite,      // Final: Combined result
        PostProcess     // Final: After color grading, gusting, etc.
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

    // Inside Upscaler.cs
    public class EditorUpscaler
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _nativeRenderTarget; // The low-res 480x270 canvas

        // --- DOUBLE BUFFER SIMULATION TARGETS ---
        private RenderTarget2D _fluidStateCurrent;
        private RenderTarget2D _fluidStatePrevious;

        public int NativeWidth { get; }
        public int NativeHeight { get; }
        public int Scale { get; }

        public RenderTarget2D FluidCurrent => _fluidStateCurrent;
        public RenderTarget2D FluidPrevious => _fluidStatePrevious;

        public EditorUpscaler(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            NativeWidth = 480;
            NativeHeight = 270;
            Scale = 2;

            _nativeRenderTarget = new RenderTarget2D(
                _graphicsDevice,
                NativeWidth,
                NativeHeight,
                false,
                _graphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24
            );

            // Instantiate low-res floating-point buffers for fluid physics stability
            _fluidStateCurrent = new RenderTarget2D(
                _graphicsDevice,
                NativeWidth,
                NativeHeight,
                false,
                SurfaceFormat.HalfVector4,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents
            );

            _fluidStatePrevious = new RenderTarget2D(
                _graphicsDevice,
                NativeWidth,
                NativeHeight,
                false,
                SurfaceFormat.HalfVector4,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents
            );
        }

        public void SwapFluidBuffers()
        {
            var temp = _fluidStateCurrent;
            _fluidStateCurrent = _fluidStatePrevious;
            _fluidStatePrevious = temp;
        }

        public void BeginRender()
        {
            _graphicsDevice.SetRenderTarget(_nativeRenderTarget);
        }

        public void Present(SpriteBatch spriteBatch, Rectangle viewportDestination)
        {
            _graphicsDevice.SetRenderTarget(null);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_nativeRenderTarget, viewportDestination, Color.White);
            spriteBatch.End();
        }
    }
    public class MicroRenderTargetPool
    {
        private GraphicsDevice _graphicsDevice;
        private List<RenderTarget2D> _pool = new List<RenderTarget2D>();
        private bool[] _inUse;
        private const int MAX_POOL_SIZE = 32;

        public MicroRenderTargetPool(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _inUse = new bool[MAX_POOL_SIZE];
        }

        public RenderTarget2D Rent(int width, int height, SurfaceFormat format, DepthFormat depthFormat)
        {
            for (int i = 0; i < MAX_POOL_SIZE; i++)
            {
                if (!_inUse[i])
                {
                    // If slot is empty, or size/format doesn't match, recreate it
                    if (_pool.Count <= i) _pool.Add(null);

                    var rt = _pool[i];
                    if (rt == null || rt.Width != width || rt.Height != height || rt.Format != format || rt.DepthStencilFormat != depthFormat)
                    {
                        rt?.Dispose();
                        // Notice PreserveContents! This ensures the Micro RT doesn't randomly clear itself.
                        rt = new RenderTarget2D(_graphicsDevice, width, height, false, format, depthFormat, 0, RenderTargetUsage.PreserveContents);
                        _pool[i] = rt;
                    }

                    _inUse[i] = true;
                    return rt;
                }
            }
            throw new System.Exception("Micro RT Pool exhausted! Increase MAX_POOL_SIZE or check for memory leaks.");
        }

        public void ReturnAll()
        {
            for (int i = 0; i < MAX_POOL_SIZE; i++) _inUse[i] = false;
        }
    }
    public class RenderPipeline
    {
        public readonly GraphicsDevice _graphicsDevice;
        public MicroRenderTargetPool MicroRTs { get; private set; }

        // Master Buffer Allocations (Maximum possible resolution)
        public const int MASTER_SIM_W = 1920;
        public const int MASTER_SIM_H = 1080;
        public const int MASTER_NAT_W = 960;
        public const int MASTER_NAT_H = 540;

        private readonly Dictionary<RenderLayer, RenderTarget2D> _targets;

        public Rectangle NativeRect { get; } = new Rectangle(0, 0, 480, 270);
        public Rectangle SimRect { get; } = new Rectangle(0, 0, 960, 540);
        public Rectangle FinalRect { get; } = new Rectangle(0, 0, 1920, 1080);

        public RenderLayer _currentDebugView = RenderLayer.Composite;
        private KeyboardState _prevKeyboardState;
        private readonly List<RenderLayer> _debugCycleOrder;

        public RenderPipeline(GraphicsDevice graphicsDevice, int finalWidth, int finalHeight)
        {
            _graphicsDevice = graphicsDevice;
            FinalRect = new Rectangle(0, 0, finalWidth, finalHeight);

            _targets = new Dictionary<RenderLayer, RenderTarget2D>();
            _debugCycleOrder = new List<RenderLayer>
        {
            RenderLayer.Composite,
            RenderLayer.Albedo,
            RenderLayer.Dynamic,
            RenderLayer.Simulation,
            RenderLayer.Normal,
            RenderLayer.YDepth,
            RenderLayer.ZDepth,
            RenderLayer.WaterData
        };

            MicroRTs = new MicroRenderTargetPool(graphicsDevice);
            InitializeTargets();
        }

        private void InitializeTargets()
        {
            // 1. MASTER NATIVE TARGETS (960x540)
            _targets[RenderLayer.Albedo] = new RenderTarget2D(_graphicsDevice, MASTER_NAT_W, MASTER_NAT_H, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            _targets[RenderLayer.Simulation] = new RenderTarget2D(_graphicsDevice, MASTER_NAT_W, MASTER_NAT_H, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

            // 2. MASTER SIMULATION TARGETS (1920x1080)
            _targets[RenderLayer.YDepth] = new RenderTarget2D(_graphicsDevice, MASTER_SIM_W, MASTER_SIM_H, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            _targets[RenderLayer.ZDepth] = new RenderTarget2D(_graphicsDevice, MASTER_SIM_W, MASTER_SIM_H, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            _targets[RenderLayer.WaterData] = new RenderTarget2D(_graphicsDevice, MASTER_SIM_W, MASTER_SIM_H, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            _targets[RenderLayer.Dynamic] = new RenderTarget2D(_graphicsDevice, MASTER_SIM_W, MASTER_SIM_H, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            _targets[RenderLayer.Normal] = new RenderTarget2D(_graphicsDevice, MASTER_SIM_W, MASTER_SIM_H, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            _targets[RenderLayer.LightMask] = new RenderTarget2D(_graphicsDevice, MASTER_SIM_W, MASTER_SIM_H, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            _targets[RenderLayer.Shader] = new RenderTarget2D(_graphicsDevice, MASTER_SIM_W, MASTER_SIM_H, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

            // 3. FINAL PRESENTATION TARGETS (1920x1080)
            _targets[RenderLayer.Composite] = new RenderTarget2D(_graphicsDevice, FinalRect.Width, FinalRect.Height, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            _targets[RenderLayer.PostProcess] = new RenderTarget2D(_graphicsDevice, FinalRect.Width, FinalRect.Height, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }
        public void SetWindowResolution(GraphicsDeviceManager graphicsManager, bool isFullScreen = false, bool borderless = false)
        {
            graphicsManager.PreferredBackBufferWidth = FinalRect.Width;   // 1920
            graphicsManager.PreferredBackBufferHeight = FinalRect.Height; // 1080
            graphicsManager.IsFullScreen = isFullScreen;
            graphicsManager.HardwareModeSwitch = !borderless; // False = Borderless Fullscreen (no resolution flicker)

            // Ensure VSync is enabled for smooth panning
            graphicsManager.SynchronizeWithVerticalRetrace = true;

            graphicsManager.ApplyChanges();

            System.Diagnostics.Debug.WriteLine($"[RenderPipeline] Window Resolution set to: {FinalRect.Width}x{FinalRect.Height} (FullScreen: {isFullScreen})");
        }

        /// <summary>
        /// Optional helper to allow arbitrary output resolutions (e.g. 1440p, 4K, 720p).
        /// </summary>
        public void SetCustomResolution(GraphicsDeviceManager graphicsManager, int width, int height, bool isFullScreen = false)
        {
            graphicsManager.PreferredBackBufferWidth = width;
            graphicsManager.PreferredBackBufferHeight = height;
            graphicsManager.IsFullScreen = isFullScreen;
            graphicsManager.ApplyChanges();
        }
        public void Begin(RenderLayer layer, Color? clearColor, Camera camera = null)
        {
            _graphicsDevice.SetRenderTarget(_targets[layer]);

            // Dynamically assign active hardware viewport to match the (A x B) camera view
            if (camera != null)
            {
                if (layer == RenderLayer.Albedo || layer == RenderLayer.Simulation)
                {
                    _graphicsDevice.Viewport = new Viewport(camera.NativeViewportRect);
                }
                else if (layer != RenderLayer.Composite && layer != RenderLayer.PostProcess)
                {
                    _graphicsDevice.Viewport = new Viewport(camera.SimViewportRect);
                }
            }

            if (clearColor.HasValue)
            {
                _graphicsDevice.Clear(clearColor.Value);
            }
        }

        public RenderTarget2D GetRenderTarget(RenderLayer layer) => _targets[layer];

        public void Update()
        {
            var kstate = Keyboard.GetState();
            if (kstate.IsKeyDown(Keys.P) && _prevKeyboardState.IsKeyUp(Keys.P))
            {
                int currentIndex = _debugCycleOrder.IndexOf(_currentDebugView);
                _currentDebugView = _debugCycleOrder[(currentIndex + 1) % _debugCycleOrder.Count];
            }
            _prevKeyboardState = kstate;
        }

        public void PresentFinal(SpriteBatch spriteBatch, Camera camera)
        {
            _graphicsDevice.SetRenderTarget(_targets[RenderLayer.Composite]);
            _graphicsDevice.Viewport = new Viewport(FinalRect);
            _graphicsDevice.Clear(Color.Black);

            // Blit native tiles and simulation entities using their active viewport sub-rectangles
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp);
            spriteBatch.Draw(_targets[RenderLayer.Albedo], FinalRect, camera.NativeViewportRect, Color.White);
            spriteBatch.Draw(_targets[RenderLayer.Simulation], FinalRect, camera.NativeViewportRect, Color.White);
            spriteBatch.Draw(_targets[RenderLayer.Dynamic], FinalRect, camera.SimViewportRect, Color.White);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp);
            spriteBatch.Draw(_targets[RenderLayer.LightMask], FinalRect, camera.SimViewportRect, Color.White);
            spriteBatch.End();
        }

        public void PostFinal(SpriteBatch spriteBatch, Camera camera, params Effect[] postEffects)
        {
            RenderTarget2D currentSource = _targets[RenderLayer.Composite];

            if (postEffects != null && postEffects.Length > 0)
            {
                RenderTarget2D[] pingPongTargets = { _targets[RenderLayer.PostProcess], _targets[RenderLayer.Composite] };

                for (int i = 0; i < postEffects.Length; i++)
                {
                    Effect effect = postEffects[i];
                    if (effect == null) continue;

                    RenderTarget2D destination = pingPongTargets[i % 2];
                    _graphicsDevice.SetRenderTarget(destination);
                    _graphicsDevice.Viewport = new Viewport(FinalRect);
                    _graphicsDevice.Clear(Color.Black);

                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, effect);
                    spriteBatch.Draw(currentSource, FinalRect, Color.White);
                    spriteBatch.End();

                    currentSource = destination;
                }
            }

            // Output to BackBuffer
            _graphicsDevice.SetRenderTarget(null);
            _graphicsDevice.Viewport = new Viewport(FinalRect);
            _graphicsDevice.Clear(Color.Black);

            if (_currentDebugView == RenderLayer.Composite || _currentDebugView == RenderLayer.PostProcess)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                spriteBatch.Draw(currentSource, FinalRect, Color.White);
                spriteBatch.End();
            }
            else
            {
                // Debug RT View: Extract active viewport
                Rectangle srcRect = (_currentDebugView == RenderLayer.Albedo || _currentDebugView == RenderLayer.Simulation)
                    ? camera.NativeViewportRect
                    : camera.SimViewportRect;

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
                spriteBatch.Draw(_targets[_currentDebugView], FinalRect, srcRect, Color.White);
                spriteBatch.End();
            }
        }
    }
}
