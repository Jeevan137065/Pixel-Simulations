using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Timers;
using Pixel_Simulations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations
{
    public class WaterSimulation
    {
        private readonly GraphicsDevice _graphicsDevice;
        private Effect _physicsShader;

        public WaterSimulation(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        public void LoadContent(ContentManager content)
        {
            _physicsShader = content.Load<Effect>("WaterPhysics");
        }

        public void StepGPUSimulation(EditorUpscaler upscaler, float dt, float damping)
        {
            if (_physicsShader == null) return;

            var prevTargets = _graphicsDevice.GetRenderTargets();

            // Pass 1: Compute wave heights on active targets
            _graphicsDevice.SetRenderTarget(upscaler.FluidCurrent);
            _physicsShader.Parameters["TexelSize"]?.SetValue(new Vector2(1f / upscaler.NativeWidth, 1f / upscaler.NativeHeight));
            _physicsShader.Parameters["Damping"]?.SetValue(damping);

            _graphicsDevice.Textures[0] = upscaler.FluidPrevious; // s0: t-1 state
            _graphicsDevice.Textures[1] = upscaler.FluidCurrent;  // s1: t state

            ExecuteFullscreenQuadPass(upscaler.FluidCurrent.Bounds, _physicsShader.CurrentTechnique.Passes["WaveUpdate"]);

            // Swap double buffers
            upscaler.SwapFluidBuffers();

            _graphicsDevice.SetRenderTargets(prevTargets);
        }

        public void GenerateWaterDataMap(RenderTarget2D destination, RenderTarget2D source, Vector2 texelSize)
        {
            if (_physicsShader == null) return;

            var prevTargets = _graphicsDevice.GetRenderTargets();
            _graphicsDevice.SetRenderTarget(destination);

            _physicsShader.Parameters["TexelSize"]?.SetValue(texelSize);
            _graphicsDevice.Textures[0] = source;

            ExecuteFullscreenQuadPass(destination.Bounds, _physicsShader.CurrentTechnique.Passes["BakeWaterData"]);

            _graphicsDevice.SetRenderTargets(prevTargets);
        }

        private void ExecuteFullscreenQuadPass(Rectangle bounds, EffectPass pass)
        {
            VertexPositionTexture[] quadVerts = new VertexPositionTexture[]
            {
                new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1))
            };

            short[] quadIndices = new short[] { 0, 1, 2, 1, 3, 2 };

            pass.Apply();
            _graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, quadVerts, 0, 4, quadIndices, 0, 2);
        }
    }
}