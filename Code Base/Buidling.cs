using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations
{
    public class Building
    {
        private List<VertexPositionTexture> _verts;
        private VertexBuffer _buffer;
        private Vector2 _pos;
        private Vector2 _size;
        private Effect _shader;

        public Building( Vector2 position, Vector2 size, GraphicsDevice device)
        {
            _pos = position;
            _size = size;


            _verts = new List<VertexPositionTexture>();
            float x = _pos.X;
            float y = _pos.Y;
            float w = _size.X;
            float h = _size.Y;
            float depthY = _pos.Y; // The depth anchor (feet)

            // Creating a simple quad. Note: Z-component stores the depthY for the shader.
            _verts.Add(new VertexPositionTexture(new Vector3(x, y - h, depthY), Vector2.Zero));
            _verts.Add(new VertexPositionTexture(new Vector3(x + w, y - h, depthY), Vector2.Zero));
            _verts.Add(new VertexPositionTexture(new Vector3(x, y, depthY), Vector2.Zero));
            _verts.Add(new VertexPositionTexture(new Vector3(x, y, depthY), Vector2.Zero));
            _verts.Add(new VertexPositionTexture(new Vector3(x + w, y - h, depthY), Vector2.Zero));
            _verts.Add(new VertexPositionTexture(new Vector3(x + w, y, depthY), Vector2.Zero));

            _buffer = new VertexBuffer(device, typeof(VertexPositionTexture), 6, BufferUsage.WriteOnly);
            _buffer.SetData(_verts.ToArray());
        }

        public void Load(ContentManager content)
        {
            _shader = content.Load<Effect>("Occluder");
        }

        public void Update()
        {

        }

        public void Draw(GraphicsDevice device, Matrix worldViewProj)
        {
            _shader.Parameters["WorldViewProjection"]?.SetValue(worldViewProj);
            // We set the depth explicitly for the building
            _shader.Parameters["PlayerOrigin"]?.SetValue(_pos.Y);
            _shader.Parameters["PlayerPos"]?.SetValue(_pos);

            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;

            foreach (var pass in _shader.CurrentTechnique.Passes)
            {
                device.SetVertexBuffer(_buffer);
                pass.Apply();
                device.DrawPrimitives(PrimitiveType.TriangleList, 0, 2);
            }
        }
    }
}
