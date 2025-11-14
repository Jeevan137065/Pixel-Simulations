using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Pixel_Simulations
{ // <-- Make sure this matches your project's namespace

    public class Upscaler
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _renderTarget; // The primary, internal canvas
        private RenderTarget2D _postProcessTarget;
        private readonly Rectangle _destinationRect;
        public int NativeWidth { get; }
        public int NativeHeight { get; }
        public int Scale { get; }

        public RenderTarget2D RenderTarget => _renderTarget;

        public Rectangle DestinationRectangle { get; private set; }
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

    }
}