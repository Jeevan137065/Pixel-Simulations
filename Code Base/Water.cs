using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
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
    public class WaterGrid
    {
        private GraphicsDevice _graphicsDevice;
        private RectangleF _bounds; // World-space boundary
        private WaterCell[,] _logicGrid;
        private int _cellSize; // 16px logical cells

        // Mesh Buffers
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private Effect _waterEffect;

        // Shader Params
        private const int MAX_RIPPLES = 16;
        public List<RippleSource> _activeRipples = new List<RippleSource>();
        private float _decayConstant = 0.8f; // Lower = fades faster
        public WaterGrid(GraphicsDevice gd, RectangleF worldBounds, int cellSize)
        {
            _graphicsDevice = gd;
            _bounds = worldBounds;
            _cellSize = cellSize;

            // Step 1: Logic
            SetupLogicGrid();

            // Step 2: Visuals
            GenerateVisualMesh(cellSize/8); // 4px spacing for high-res waves
        }

        private void SetupLogicGrid()
        {
            int cols = (int)(_bounds.Width / _cellSize);
            int rows = (int)(_bounds.Height / _cellSize);
            _logicGrid = new WaterCell[cols, rows];

            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    // Calculate "Shoreline" - distance to the nearest edge of the rectangle
                    float distToEdgeX = Math.Min(x, cols - 1 - x) * _cellSize;
                    float distToEdgeY = Math.Min(y, rows - 1 - y) * _cellSize;
                    float distToEdge = Math.Min(distToEdgeX, distToEdgeY);

                    // Smooth slope: depth increases for the first 48 pixels from edge
                    float depth = MathHelper.Clamp(distToEdge / 48f, 0, 1);
                    _logicGrid[x, y] = new WaterCell(depth, 20f);
                }
            }
        }
        private void GenerateVisualMesh(int meshSpacing)
        {
            int meshCols = (int)(_bounds.Width / meshSpacing) + 1;
            int meshRows = (int)(_bounds.Height / meshSpacing) + 1;
            VertexWater[] vertices = new VertexWater[meshCols * meshRows];

            for (int y = 0; y < meshRows; y++)
            {
                for (int x = 0; x < meshCols; x++)
                {
                    Vector2 worldCoords = new Vector2(_bounds.X + (x * meshSpacing), _bounds.Y + (y * meshSpacing));

                    // Find corresponding logic cell
                    int cellX = (int)((worldCoords.X - _bounds.X) / _cellSize);
                    int cellY = (int)((worldCoords.Y - _bounds.Y) / _cellSize);
                    cellX = MathHelper.Clamp(cellX, 0, _logicGrid.GetLength(0) - 1);
                    cellY = MathHelper.Clamp(cellY, 0, _logicGrid.GetLength(1) - 1);

                    float depth = _logicGrid[cellX, cellY].Depth;

                    // Pass Depth as Density (input.Density in shader)
                    vertices[y * meshCols + x] = new VertexWater(new Vector3(worldCoords, 0), depth);
                }
            }

            // 2. Create Index Data (Triangle List)
            int indexCount = (meshCols - 1) * (meshRows - 1) * 6;
            short[] indices = new short[indexCount];
            int counter = 0;
            for (int y = 0; y < meshRows - 1; y++)
            {
                for (int x = 0; x < meshCols - 1; x++)
                {
                    short topLeft = (short)(y * meshCols + x);
                    short topRight = (short)(topLeft + 1);
                    short bottomLeft = (short)((y + 1) * meshCols + x);
                    short bottomRight = (short)(bottomLeft + 1);

                    indices[counter++] = topLeft;
                    indices[counter++] = topRight;
                    indices[counter++] = bottomLeft;
                    indices[counter++] = bottomLeft;
                    indices[counter++] = topRight;
                    indices[counter++] = bottomRight;
                }
            }

            // 3. Initialize GPU Buffers
            _vertexBuffer = new DynamicVertexBuffer(_graphicsDevice, typeof(VertexWater), vertices.Length, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(vertices);

            _indexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);
        }

        public void Update(Player player, GameTime gameTime)
        {
            float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;

            // 1. Boundary & Depth Check
            if (_bounds.Contains(player.Foot))
            {
                int cx = (int)((player.Foot.X - _bounds.X) / _cellSize);
                int cy = (int)((player.Foot.Y - _bounds.Y) / _cellSize);
                cx = MathHelper.Clamp(cx, 0, _logicGrid.GetLength(0) - 1);
                cy = MathHelper.Clamp(cy, 0, _logicGrid.GetLength(1) - 1);

                float localDepth = _logicGrid[cx, cy].Depth;

                // Goal B: Sink the player
                player.Sink(localDepth);

                // Create ripple if moving in water
                if (player.isMoving && localDepth == player.SubmergedAmount)
                {
                    //if (_activeRipples.Count < MAX_RIPPLES)
                    if (_activeRipples.Count == 0 || (totalTime - _activeRipples.Last().StartTime > 0.8f))
                    {
                        // Power is based on player speed
                        float power = player.Velocity.Length() / 160f;
                        AddRipple(new Vector3(player.Foot, 1.0f), player.Velocity, totalTime, power);
                    }
                }
            }
            else
            {
                player.Sink(0); // Player is on land
            }

        }

        private void AddRipple(Vector3 pos, Vector2 velocity, float time, float power)
        {
            if (_activeRipples.Count >= MAX_RIPPLES) _activeRipples.RemoveAt(0);
            _activeRipples.Add(new RippleSource
            {
                Position = pos,
                Velocity = velocity,
                StartTime = time,
                InitialPower = power
            });
        }
        public void Load(ContentManager content)
        {
            _waterEffect = content.Load<Effect>("WaterPhysics");
        }


        public void Draw(Matrix viewProjection, GameTime gameTime)
        {
            _waterEffect.Parameters["WorldViewProjection"].SetValue(viewProjection);
            _waterEffect.Parameters["Time"].SetValue((float)gameTime.TotalGameTime.TotalSeconds);
            //_waterEffect.Parameters["GridSpacing"].SetValue(_cellSize);
            // Pass the ripple array to the shader
            Vector4[] rippleData = new Vector4[MAX_RIPPLES]; // x,y,z=pos, w=power
        Vector4[] rippleMisc = new Vector4[MAX_RIPPLES]; // x,y=vel, z=starttime, w=unused

        for (int i = 0; i < MAX_RIPPLES; i++)
        {
            if (i < _activeRipples.Count)
            {
                var r = _activeRipples[i];
                rippleData[i] = new Vector4(r.Position.X, r.Position.Y, r.Position.Z, r.InitialPower);
                rippleMisc[i] = new Vector4(r.Velocity.X, r.Velocity.Y, r.StartTime, 0);
            }
            else
            {
                rippleData[i] = Vector4.Zero;
                rippleMisc[i] = new Vector4(0, 0, -100, 0); // Far in the past
            }
        }

            _waterEffect.Parameters["RippleData"].SetValue(rippleData);
            _waterEffect.Parameters["RippleMisc"].SetValue(rippleMisc);

            _graphicsDevice.SetVertexBuffer(_vertexBuffer);
            _graphicsDevice.Indices = _indexBuffer;

            foreach (var pass in _waterEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _indexBuffer.IndexCount / 3);
            }
        }
    }
}