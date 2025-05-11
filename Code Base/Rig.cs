// Rig.cs
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Pixel_Simulations
{
    public class Rig
    {
        public readonly int Width = 30, Height = 50;
        public float Scale = 8f;
        private readonly Vector2 _gravity = new Vector2(0, 0.2f);

        public class Node
        {
            public Vector2 Center;
            public Vector2 Velocity;
            public Color Color;
            public int Size;
            public float Mass;

            public Node(Vector2 center, Color col, int size, float mass = 1f)
            {
                Center = center;
                Color = col;
                Size = size;
                Mass = mass;
                Velocity = Vector2.Zero;
            }
        }

        public class Bone
        {
            public Node A, B;
            public float RestLength;
            public float Stiffness;
            public Color Color;

            public Bone(Node a, Node b, float stiffness, Color color)
            {
                A = a;
                B = b;
                RestLength = Vector2.Distance(a.Center, b.Center);
                Stiffness = stiffness;
                Color = color;
            }
        }

        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<Bone> _bones = new List<Bone>();
        private readonly Texture2D _pixel;

        public Rig(GraphicsDevice gd)
        {
            _pixel = new Texture2D(gd, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // nodes: x, y, size, mass
            AddNode(14, 5, 2, 2f, Color.Red);     // Head
            AddNode(14, 12, 2, 1f, Color.Orange);  // Neck
            AddNode(12, 17, 1, 2f, Color.Yellow);  // ChestR
            AddNode(17, 17, 1, 2f, Color.Yellow);  // ChestL
            AddNode(8, 16, 2, 1f, Color.Green);   // ShoulderR
            AddNode(20, 16, 2, 1f, Color.Green);   // ShoulderL
            AddNode(5, 23, 2, 0.5f, Color.Cyan);  // ArmR
            AddNode(23, 23, 2, 0.5f, Color.Cyan);  // ArmL
            AddNode(14, 24, 2, 5f, Color.Blue);    // COG (heavy)
            AddNode(4, 32, 2, 0.3f, Color.Purple);// HandR
            AddNode(24, 32, 2, 0.3f, Color.Purple);// HandL
            AddNode(10, 30, 2, 2f, Color.Magenta);// ThighR
            AddNode(18, 30, 2, 2f, Color.Magenta);// ThighL
            AddNode(9, 38, 2, 1f, Color.Brown);   // KneeR
            AddNode(19, 38, 2, 1f, Color.Brown);   // KneeL
            AddNode(7, 47, 3, 3f, Color.Black);   // FootR
            AddNode(20, 47, 3, 3f, Color.Black);   // FootL

            // bones: indexA, indexB, stiffness, color
            Connect(0, 1, 0.9f, Color.LightCoral);
            Connect(1, 2, 0.8f, Color.LightYellow);
            Connect(1, 3, 0.8f, Color.LightYellow);
            Connect(2, 4, 0.7f, Color.LightGreen);
            Connect(3, 5, 0.7f, Color.LightGreen);
            Connect(4, 6, 0.5f, Color.Cyan);
            Connect(5, 7, 0.5f, Color.Cyan);
            Connect(6, 9, 0.4f, Color.Purple);
            Connect(7, 10, 0.4f, Color.Purple);
            Connect(1, 8, 1.2f, Color.Blue);
            Connect(8, 11, 0.6f, Color.Magenta);
            Connect(8, 12, 0.6f, Color.Magenta);
            Connect(11, 13, 0.5f, Color.Brown);
            Connect(12, 14, 0.5f, Color.Brown);
            Connect(13, 15, 0.6f, Color.Black);
            Connect(14, 16, 0.6f, Color.Black);
        }

        private void AddNode(int x, int y, int size, float mass, Color col)
        {
            var center = new Vector2(x + size * 0.5f, y + size * 0.5f);
            _nodes.Add(new Node(center, col, size, mass));
        }

        private void Connect(int a, int b, float stiffness, Color col)
        {
            _bones.Add(new Bone(_nodes[a], _nodes[b], stiffness, col));
        }

        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // apply gravity
            foreach (var n in _nodes)
            {
                n.Velocity += _gravity * dt / n.Mass;
            }

            // spring forces
            foreach (var bone in _bones)
            {
                Vector2 delta = bone.B.Center - bone.A.Center;
                float dist = delta.Length();
                float diff = dist - bone.RestLength;
                Vector2 dir = delta / dist;
                Vector2 force = bone.Stiffness * diff * dir;

                bone.A.Velocity += force / bone.A.Mass * dt;
                bone.B.Velocity += -force / bone.B.Mass * dt;
            }

            // damping + integrate
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                n.Velocity *= 0.98f;
                n.Center += n.Velocity * dt;
            }
        }

        public void Draw(SpriteBatch sb, Vector2 offset)
        {
            // bones
            foreach (var bone in _bones)
            {
                //DrawLine(sb,bone.A.Center * Scale + offset,bone.B.Center * Scale + offset,bone.Color,thickness: 1f * Scale);
            }

            // nodes
            foreach (var n in _nodes)
            {
                var p = n.Center * Scale + offset;
                float s = n.Size * Scale;
                sb.Draw(
                    _pixel,
                    new Rectangle((int)(p.X - s / 2), (int)(p.Y - s / 2), (int)s, (int)s),
                    n.Color
                );
            }
        }

        private void DrawLine(SpriteBatch sb, Vector2 a, Vector2 b, Color col, float thickness)
        {
            var delta = b - a;
            float len = delta.Length();
            float ang = (float)Math.Atan2(delta.Y, delta.X);
            sb.Draw(
              _pixel, a, null, col, ang,
              new Vector2(0, 0.5f),
              new Vector2(len, thickness),
              SpriteEffects.None, 0f
            );
        }
    }
}
