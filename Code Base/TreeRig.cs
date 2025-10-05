// TreeRig.cs
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Pixel_Simulations.Code_Base;

namespace Pixel_Simulations
{
    public class TreeRig
    {
        // Canvas dimensions
        public const int Width = 120;
        public const int Height = 128;
        public float Scale = 4f;

        // Physics constants
        private static readonly Vector2 Gravity = new Vector2(0, 0.2f);
        private const float SpringK = 1.0f;
        private const float SpringDamp = 0.1f;
        private const float WindStrength = 40f;
        private const float WindInterval = 2f;
        private const float WindFrequency = 0.5f;    // oscillations per second
        private const float NodeDamping = 0.5f;     // velocity retention (0–1)

        

        // Instance fields
        private readonly List<Node> _nodes;
        private readonly List<Bone> _bones;
        private readonly Dictionary<char, Texture2D> _leafTextures;
        private readonly Texture2D _pixel;
        private readonly float[] _restAngles;

        private readonly int[] _anchorIndices = new[] { 0,1 };

        // Wind state
        private float _windTimer = 0;
        private int _windDir = 1;

        public TreeRig(GraphicsDevice gd, ContentManager cm)
        {
            // 1px white pixel for line-drawing
            _pixel = new Texture2D(gd, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Create nodes (a–p)
            _nodes = new List<Node>();
            void N(int x, int y, int s)
            {
                var center = new Vector2(x + s / 2f, y + s / 2f);
                _nodes.Add(new Node(center));
            }
            N(62, 121, 4); N(65, 87, 3); N(50, 62, 2); N(72, 72, 3);
            N(80, 58, 2); N(72, 68, 2); N(65, 49, 2); N(76, 60, 2);
            N(55, 41, 2); N(65, 39, 1); N(75, 36, 1); N(37, 56, 1);
            N(47, 41, 2); N(25, 29, 1); N(46, 33, 2); N(54, 32, 1);

            // Create bones A–O with explicit pivots
            _bones = new List<Bone>();
            void B(int a, int b, Vector2 pivot)
                => _bones.Add(new Bone(_nodes[a], _nodes[b], Color.DarkGreen, pivot));

            // pivots are local coords in each 120×128 sprite where it attaches
            B(0, 1, new Vector2(62f, 121f));  // Bone A
            B(1, 2, new Vector2(65f, 87f));  // Bone B
            B(1, 3, new Vector2(65f, 87f));  // Bone C
            B(3, 5, new Vector2(72f, 72f));  // D
            B(3, 4, new Vector2(72f, 72f));  // E
            B(5, 7, new Vector2(72f, 68f));  // F
            B(5, 6, new Vector2(72f, 68f));  // G
            B(6, 9, new Vector2(65f, 49f));  // H
            B(6, 8, new Vector2(65f, 49f));  // I
            B(7, 10, new Vector2(76f, 60f));  // J
            B(2, 12, new Vector2(50f, 62f));  // K
            B(2, 11, new Vector2(50f, 62f));  // L
            B(12, 13, new Vector2(47f, 41f));  // M
            B(12, 15, new Vector2(47f, 41f));  // N
            B(12, 14, new Vector2(47f, 41f));  // O

            // Load leaf textures Tree_A…Tree_O
            _leafTextures = new Dictionary<char, Texture2D>();
            for (char c = 'A'; c <= 'O'; c++)
                _leafTextures[c] = cm.Load<Texture2D>($"Tree_{c}");

            // Store rest angles
            _restAngles = new float[_bones.Count];
            for (int i = 0; i < _bones.Count; i++)
            {
                var b = _bones[i];
                Vector2 pa = b.A.Position;
                Vector2 pb = b.B.Position;
                _restAngles[i] = (float)Math.Atan2(pb.Y - pa.Y, pb.X - pa.X);
            }
        }

        public void Update(GameTime gt, MouseState ms)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            float t = (float)gt.TotalGameTime.TotalSeconds;

            // Wind oscillation
            _windTimer += dt;
            if (_windTimer > WindInterval)
            {
                _windTimer = 0f;
                _windDir *= -1;
            }
            float windForce = WindStrength * (float)Math.Sin(2 * Math.PI * WindFrequency * t);
            Vector2 wind = new Vector2(windForce, 0f);

            // 1) External forces + damping, anchoring base nodes
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                if (Array.IndexOf(_anchorIndices, i) >= 0)
                {
                    n.Position = n.RestPosition;
                    n.Velocity = Vector2.Zero;
                    continue;
                }
                n.Velocity += Gravity * dt / n.Mass;
                float heightFactor = 1f - (n.Position.Y / (Height * Scale));
                n.Velocity += wind * heightFactor * dt / n.Mass;
                n.Position += n.Velocity * dt;
                n.Velocity *= NodeDamping;
            }

            // 2) Spring–constraint projection for each bone
            foreach (var b in _bones)
            {
                Vector2 delta = b.B.Position - b.A.Position;
                float dist = delta.Length();
                if (dist == 0) continue;
                float diff = dist - b.RestLength;
                Vector2 dir = delta / dist;
                Vector2 corr = dir * (diff * 0.5f);
                // apply projection, skip anchors
                int aIdx = _nodes.IndexOf(b.A);
                int bIdx = _nodes.IndexOf(b.B);
                if (Array.IndexOf(_anchorIndices, aIdx) < 0)
                    b.A.Position += corr;
                if (Array.IndexOf(_anchorIndices, bIdx) < 0)
                    b.B.Position -= corr;
            }
        }

        /// <summary>
        /// Draw each leaf texture rotated by bones’ delta angle around its pivot.
        /// </summary>
        public void DrawTextures(SpriteBatch sb, Vector2 rigOrigin)
        {
            for (int i = 0; i < _bones.Count; i++)
            {
                var b = _bones[i];
                char key = (char)('A' + i);
                var tex = _leafTextures[key];

                Vector2 pA = b.A.Position * Scale + rigOrigin;
                Vector2 pB = b.B.Position * Scale + rigOrigin;
                float curr = (float)Math.Atan2(pB.Y - pA.Y, pB.X - pA.X);
                float delta = curr - _restAngles[i];

                sb.Draw(
                    tex,
                    pA - (b.PivotLocal * Scale),
                    null,
                    Color.White,
                    delta,
                    Vector2.Zero,
                    new Vector2(Scale, Scale),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        public void DrawBones(SpriteBatch sb, Vector2 rigOrigin)
        {
            foreach (var b in _bones)
            {
                Vector2 a = b.A.Position * Scale + rigOrigin;
                Vector2 c = b.B.Position * Scale + rigOrigin;
                DrawLine(sb, a, c, b.Color, 5f);
            }
        }

        public void DrawNodes(SpriteBatch sb, Vector2 rigOrigin)
        {
            foreach (var n in _nodes)
            {
                var p = n.Position * Scale + rigOrigin;
                float s = 4f * Scale;
                sb.Draw(_pixel,
                        new Rectangle((int)(p.X - s / 2), (int)(p.Y - s / 2), (int)s, (int)s),
                        Color.Red);
            }
        }

        private void DrawLine(SpriteBatch sb, Vector2 p1, Vector2 p2, Color color, float thickness)
        {
            Vector2 delta = p2 - p1;
            float len = delta.Length();
            float ang = (float)Math.Atan2(delta.Y, delta.X);
            sb.Draw(_pixel,
                    p1,
                    null,
                    color,
                    ang,
                    new Vector2(0, 0.5f),
                    new Vector2(len, thickness),
                    SpriteEffects.None,
                    0f);
        }
    }
}
