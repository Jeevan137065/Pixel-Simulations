// Rig.cs
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Pixel_Simulations
{
    public class Rig
    {
        public const int Width = 30, Height = 50;
        public float Scale = 4f;

        private static readonly Vector2 Gravity = new Vector2(0, 200f);
        private const float PoseK = 5f, PoseD = 0.8f;
        private const float MouseForce = 5000f, MouseRadius = 50f;

        public bool ShowDebug = false;
        public bool ShowForceField = false;

        private bool _breathing = false;
        private bool _headLook = false;

        public class Node
        {
            public Vector2 Center, Velocity, BindCenter;
            public Color Color;
            public int Size;
            public float Mass;

            public Node(Vector2 center, Color color, int size, float mass)
            {
                Center = center;
                BindCenter = center;
                Velocity = Vector2.Zero;
                Color = color;
                Size = size;
                Mass = mass;
            }
        }

        public class Bone
        {
            public Node A, B;
            public float RestLength, Stiffness;
            public Color Color;

            public Bone(Node a, Node b, float stiffness, Color color)
            {
                A = a; B = b;
                RestLength = Vector2.Distance(a.Center, b.Center);
                Stiffness = stiffness;
                Color = color;
            }
        }

        public List<Node> _nodes;
        public List<Bone> _bones;
        public Texture2D _pixel;

        public Rig(GraphicsDevice gd)
        {
            _pixel = new Texture2D(gd, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _nodes = RigData.CreateNodes();
            _bones = RigData.CreateBones(_nodes);
        }


        public void Update(GameTime gt, MouseState ms)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;

            //if (ShowForceField) ApplyMouseForce(ms);
            ApplyBonePhysics(dt);
            //EnforceGroundPlane();
            ApplyPoseSprings(dt);
            ApplyProcedural(gt, ms);
        }

        public void Draw(SpriteBatch sb, Vector2 off, MouseState ms)
        {
            DrawBones(sb, off);
            DrawNodes(sb, off);
            
            if (ShowDebug) DrawDebug(sb, off, ms);
        }

        public void ApplyBonePhysics(float dt)
        {
            foreach (var b in _bones)
            {
                var delta = b.B.Center - b.A.Center;
                float dist = delta.Length();
                var dir = delta / dist;
                float diff = dist - b.RestLength;
                var force = dir * (b.Stiffness * diff);
                b.A.Velocity += force / b.A.Mass * dt;
                b.B.Velocity -= force / b.B.Mass * dt;
            }
        }

        private void EnforceGroundPlane()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                if (n.Center.Y > Height)
                {
                    n.Center.Y = Height;
                    n.Velocity.Y *= -0.3f;
                }
            }
        }

        public void ApplyPoseSprings(float dt)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (RigData.IsFoot(i))
                {
                    var n = _nodes[i];
                    n.Center = n.BindCenter;
                    n.Velocity = Vector2.Zero;
                    continue;
                }
                var n2 = _nodes[i];
                var err = n2.BindCenter - n2.Center;
                var ded = -n2.Velocity;
                var P = err * PoseK;
                var D = ded * (PoseK * PoseD);
                n2.Velocity += (P + D) / n2.Mass * dt;
                n2.Velocity *= 0.98f;
                n2.Center += n2.Velocity * dt;
            }
        }

        private void ApplyProcedural(GameTime gt, MouseState ms)
        {
            if (_breathing)
            {
                float sway = (float)Math.Sin(gt.TotalGameTime.TotalSeconds * 2f) * 0.2f;
                _nodes[RigData.ArmR].Center.X = _nodes[RigData.ArmR].BindCenter.X + sway;
                _nodes[RigData.ArmL].Center.X = _nodes[RigData.ArmL].BindCenter.X - sway;
            }
            if (_headLook)
            {
                var mw = new Vector2(ms.X, ms.Y) / Scale;
                var head = _nodes[RigData.Head];
                var dir = Vector2.Normalize(mw - head.Center);
                head.Center = head.BindCenter + dir * 0.5f;
            }
        }

        private void DrawBones(SpriteBatch sb, Vector2 off)
        {
            foreach (var b in _bones)
            {
                var a = b.A.Center * Scale + off;
                var c = b.B.Center * Scale + off;
                DrawLine(sb, a, c, b.Color, Scale);
            }
        }

        public void DrawNodes(SpriteBatch sb, Vector2 off)
        {
            foreach (var n in _nodes)
            {
                var p = n.Center * Scale + off;
                float s = n.Size * Scale;
                sb.Draw(_pixel,
                    new Rectangle((int)(p.X - s / 2), (int)(p.Y - s / 2), (int)s, (int)s),
                    n.Color);
            }
        }

        private void DrawDebug(SpriteBatch sb, Vector2 off, MouseState ms)
        {
            var rect = new Rectangle((int)off.X, (int)off.Y,
                                     (int)(Width * Scale), (int)(Height * Scale));
            DrawOutline(sb, rect, Color.White * 0.2f);
            if (ShowForceField)
            {
                var center = new Vector2(ms.X, ms.Y);
                float r = MouseRadius;
                for (int a = 0; a < 360; a += 10)
                {
                    float rad = MathHelper.ToRadians(a);
                    var p = center + new Vector2((float)Math.Cos(rad), (float)Math.Sin(rad)) * r;
                    sb.Draw(_pixel, new Rectangle((int)p.X, (int)p.Y, 2, 2), Color.Red);
                }
            }
        }

        private void DrawOutline(SpriteBatch sb, Rectangle r, Color c)
        {
            sb.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, 1), c);
            sb.Draw(_pixel, new Rectangle(r.X, r.Y, 1, r.Height), c);
            sb.Draw(_pixel, new Rectangle(r.X, r.Y + r.Height - 1, r.Width, 1), c);
            sb.Draw(_pixel, new Rectangle(r.X + r.Width - 1, r.Y, 1, r.Height), c);
        }

        public void DrawLine(SpriteBatch sb, Vector2 a, Vector2 b, Color col, float thickness)
        {
            var delta = b - a;
            float len = delta.Length();
            float ang = (float)Math.Atan2(delta.Y, delta.X);
            sb.Draw(_pixel, a, null, col, ang,
                    new Vector2(0, 0.5f), new Vector2(len, thickness),
                    SpriteEffects.None, 0f);
        }
    }
}