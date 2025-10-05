using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Pixel_Simulations
{ // <-- Make sure this matches your project's namespace

    public class Upscaler
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _renderTarget;

        public int NativeWidth { get; }
        public int NativeHeight { get; }
        public int Scale { get; }

        public RenderTarget2D RenderTarget => _renderTarget;

        public Rectangle DestinationRectangle { get; private set; }
        public Upscaler(GraphicsDevice graphicsDevice, int nativeWidth, int nativeHeight, int scale)
        {
            _graphicsDevice = graphicsDevice;
            NativeWidth = nativeWidth;
            NativeHeight = nativeHeight;
            Scale = scale;
        }

        public void LoadContent()
        {
            // Create the RenderTarget2D. This is the "canvas" we'll draw our low-res game to.
            _renderTarget = new RenderTarget2D(
                _graphicsDevice,
                NativeWidth,
                NativeHeight,
                false, // Mipmap
                _graphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);
        }

        //public void SetWindowResolution(GraphicsDeviceManager graphics)
        //{
        //    int finalWidth = NativeWidth * Scale;
        //    int finalHeight = NativeHeight * Scale;

        //    graphics.PreferredBackBufferWidth = finalWidth;
        //    graphics.PreferredBackBufferHeight = finalHeight;
        //    graphics.IsFullScreen = true;
        //    graphics.ApplyChanges();

        //    DestinationRectangle = new Rectangle(0, 0, finalWidth, finalHeight);
        //}
        // NOTE: We will NOT use SetWindowResolution for the editor, as it's for fullscreen games.


        public void BeginRender()
        {
            _graphicsDevice.SetRenderTarget(_renderTarget);
        }


        public void Present(SpriteBatch spriteBatch)
        {
            // 1. Reset the render target from our low-res canvas back to the screen (the back buffer)
            _graphicsDevice.SetRenderTarget(null);
            _graphicsDevice.Clear(Color.Black); // Clear the screen to black for letterboxing if needed

            // 2. Draw our low-res canvas to the screen, upscaled.
            spriteBatch.Begin(
                sortMode: SpriteSortMode.Immediate,
                blendState: BlendState.Opaque,
                samplerState: SamplerState.PointClamp, // IMPORTANT: This prevents blurring!
                depthStencilState: null,
                rasterizerState: null);

            // Draw the contents of the render target to the whole screen
            spriteBatch.Draw(_renderTarget, DestinationRectangle, Color.White);

            spriteBatch.End();
        }
    }
}