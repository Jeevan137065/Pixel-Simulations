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
    public struct VertexWater : IVertexType
    {
        public Vector3 Position; // x, y, z
        public float Density;    // a (Passed as a texcoord or color)

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration
        (
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 0)
        );

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

        public VertexWater(Vector3 pos, float density)
        {
            Position = pos;
            Density = density;
        }
    }
    public struct RippleSource
    {
        public Vector3 Position;
        public Vector2 Velocity; // Direction of the trail
        public float StartTime;
        public float InitialPower;
    }
    public struct WaterCell
    {
        public float Depth;       // 0.0 = Dry/Land, 1.0 = Full Depth
        public float Temperature; // Can affect color or steam effects later
        public bool IsActive;     // Is this part of the pond or a hole in the middle?

        public WaterCell(float depth, float temp)
        {
            Depth = depth;
            Temperature = temp;
            IsActive = depth > 0.01f;
        }
    }
    public class WaterBody
    {
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
 
        private Vector2[] _ripplePos = new Vector2[32];
        private float[] _rippleTime = new float[32];
        private float[] _ripplePower = new float[32];
        private int _rippleIndex = 0;

        private Rectangle _bounds;
        private int _columns, _rows;
        private const int GridSpacing = 8;
        private Effect waterShader;
        private bool wireFrame = false;
        private Texture2D Noise;
        private GraphicsDevice gd;

        // Trail Data (Current moving source)
        private Vector2 _playerPos;
        private Vector2 _playerVelocity;
        private float _isMoving; // 0 or 1 for shader logic

        private bool _wasMovingLastFrame = false;
        public WaterBody(GraphicsDevice _gd, Rectangle bounds)
        {
            gd = _gd;
            _bounds = bounds;
            _columns = bounds.Width / GridSpacing;
            _rows = bounds.Height / GridSpacing;

            BuildMesh(gd);
        }

        private void BuildMesh(GraphicsDevice gd)
        {
            int vertexCount = (_columns + 1) * (_rows + 1);
            var vertices = new VertexWater[vertexCount];

            for (int y = 0; y <= _rows; y++)
            {
                for (int x = 0; x <= _columns; x++)
                {
                    Vector3 pos = new Vector3(_bounds.X + x * GridSpacing, _bounds.Y + y * GridSpacing, 0);
                    vertices[y * (_columns + 1) + x] = new VertexWater(pos, 1.0f);
                }
            }

            int indexCount = _columns * _rows * 6;
            short[] indices = new short[indexCount];
            int counter = 0;
            for (int y = 0; y < _rows; y++)
            {
                for (int x = 0; x < _columns; x++)
                {
                    short tl = (short)(y * (_columns + 1) + x);
                    short tr = (short)(tl + 1);
                    short bl = (short)((y + 1) * (_columns + 1) + x);
                    short br = (short)(bl + 1);
                    indices[counter++] = tl; indices[counter++] = tr; indices[counter++] = bl;
                    indices[counter++] = bl; indices[counter++] = tr; indices[counter++] = br;
                }
            }

            _vertexBuffer = new VertexBuffer(gd, typeof(VertexWater), vertexCount, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(vertices);
            _indexBuffer = new IndexBuffer(gd, IndexElementSize.SixteenBits, indexCount, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);
        }

        private void AddRipple(Vector2 pos, float power, float time)
        {
            _ripplePos[_rippleIndex] = pos;
            _rippleTime[_rippleIndex] = time;
            _ripplePower[_rippleIndex] = power;

            // Stack management: Circular increment capped at 32
            _rippleIndex = (_rippleIndex + 1) % 32;
        }
        public void Load(ContentManager content)
        {
            waterShader = content.Load<Effect>("WaterPhysics");
            Noise = content.Load<Texture2D>("noise");
        }

        public void Update(GameTime gameTime, Player player)
        {
            float time = (float)gameTime.TotalGameTime.Seconds;
            bool inWater = _bounds.Contains(player.Foot);

            _playerPos = player.Foot;
            _playerVelocity = player.Velocity;
            if (inWater)
            {
                // Logic: Trigger Ripples on State Change
                if (player.isMoving && !_wasMovingLastFrame)
                {
                    AddRipple(player.Foot, 2.5f, time); // Start movement ripple
                }
                else if (!player.isMoving && _wasMovingLastFrame)
                {
                    AddRipple(player.Foot, 1.8f, time); // Stop movement ripple
                }

                _isMoving = player.isMoving ? 1.0f : 0.0f;

                player.Sink(0.5f);
            }
            else
            {
                _isMoving = 0.0f;
                player.Sink(0);
            }

            waterShader.Parameters["PlayerPos"].SetValue(_playerPos);
            //waterShader.Parameters["PlayerVel"].SetValue(_playerVelocity);
            waterShader.Parameters["IsMoving"].SetValue(_isMoving);
            if (Keyboard.GetState().IsKeyDown(Keys.F1))
            {
                wireFrame = !wireFrame;
            }
           
        }

        public void Draw(Matrix viewProj, GameTime time)
        {
            
            // Convert List to Shader Array
            waterShader.Parameters["WorldViewProjection"].SetValue(viewProj);
            waterShader.Parameters["Time"].SetValue((float)time.TotalGameTime.Seconds);
            waterShader.Parameters["RipplePos"].SetValue(_ripplePos);
            waterShader.Parameters["RippleTime"].SetValue(_rippleTime);
            waterShader.Parameters["RipplePower"].SetValue(_ripplePower);
            waterShader.Parameters["NoiseTexture"].SetValue(Noise);

            gd.SetVertexBuffer(_vertexBuffer);
            gd.Indices = _indexBuffer;

            var oldState = gd.RasterizerState;
            if (wireFrame)  {gd.RasterizerState = new RasterizerState { FillMode = FillMode.WireFrame };}
            foreach (var pass in waterShader.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _vertexBuffer.VertexCount, 0, _indexBuffer.IndexCount / 3);
            }
            if (wireFrame) {gd.RasterizerState = oldState;}

           // gd.SetRenderTargets(null);
        }
    }
}