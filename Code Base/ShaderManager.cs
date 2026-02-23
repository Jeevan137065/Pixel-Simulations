
// File: ShaderManager.cs
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Pixel_Simulations
{
    public class ShaderManager
    {
        private GraphicsDevice _graphicsDevice;
        private Texture2D _pixelTexture;
        private VertexPositionTexture[] _fullScreenQuad;

        // Future Shaders
        public Effect ColorGrading { get; private set; }
        public Effect Gusting { get; private set; }
        public Texture2D PerlinNoise { get; private set; }

        public ShaderManager(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            _fullScreenQuad = new VertexPositionTexture[]
            {
                new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1))
            };
        }

        public void LoadContent(ContentManager content)
        {
            ColorGrading = content.Load<Effect>("ColorGrading");
            Gusting = content.Load<Effect>("Gusting");
            PerlinNoise = content.Load<Texture2D>("PerlinA");

            // Set the noise texture once, it doesn't change
            Gusting.Parameters["NoiseTexture"].SetValue(PerlinNoise);
        }
        public void UpdatePostProcessing(WeatherVisualParams weatherParams, GameTime gameTime)
        {
            float time = (float)gameTime.TotalGameTime.TotalSeconds;

            // --- Update Gusting ---
            Gusting.Parameters["Time"].SetValue(time);
            Gusting.Parameters["WindVector"].SetValue(weatherParams.WindVector);

            // Distort more if it's raining or very windy
            float distortion = weatherParams.WindVector.Length() * 0.0001f;
            Gusting.Parameters["DistortionStrength"].SetValue(distortion);

            // --- Update Color Grading ---
            // Example: Darker and bluer during a storm
            if (weatherParams.StormIntensity > 0)
            {
                ColorGrading.Parameters["Saturation"].SetValue(0.6f);
                ColorGrading.Parameters["Brightness"].SetValue(0.8f);
                ColorGrading.Parameters["TintColor"].SetValue(new Vector3(0.7f, 0.8f, 1.0f)); // Blueish
            }
            else
            {
                // Sunny Default
                ColorGrading.Parameters["Saturation"].SetValue(1.1f);
                ColorGrading.Parameters["Brightness"].SetValue(1.0f);
                ColorGrading.Parameters["TintColor"].SetValue(Vector3.One);
            }
        }
        public void DrawWeather(RenderPipeline pipeline, WeatherVisualParams weatherParams, GameTime gameTime)
        {
            // 1. Prepare the Weather Render Target
            pipeline.Begin(RenderLayer.Shader, Color.Transparent);
            _graphicsDevice.BlendState = BlendState.AlphaBlend;

            //// 2. DRAW RAIN ON DEMAND
            //if (weatherParams.RainIntensity > 0.01f && RainShader != null)
            //{
            //    // We will set shader parameters here next based on weatherParams.RainIntensity
            //    RainShader.CurrentTechnique.Passes[0].Apply();
            //    _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, _fullScreenQuad, 0, 2);
            //}

            //// 3. DRAW SNOW ON DEMAND
            //if (weatherParams.SnowIntensity > 0.01f && SnowShader != null)
            //{
            //    // SnowShader.CurrentTechnique.Passes[0].Apply();
            //    // _graphicsDevice.DrawUserPrimitives(...);
            //}

            // Reset
            _graphicsDevice.BlendState = BlendState.Opaque;
        }
    }
}
