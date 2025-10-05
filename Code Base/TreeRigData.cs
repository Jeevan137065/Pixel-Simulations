using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Pixel_Simulations;

namespace Pixel_Simulations.Code_Base
{
    public class TreeRigData
    {
        
    }


    public class Flora
    {
        public string TreeName;
        public string FloraType;
        public int Width;
        public int Height;
        public float Scale;
        List<Node> _nodes;
        List<Bone> _bones;
        private Dictionary<char, Texture2D> _leafTextures;
        private Texture2D _pixel;
        private float[] _restAngles;
        private int[] _anchorIndices;

        public Flora(string name, string type, int w, int h, int s) 
        {
            TreeName = name;    FloraType = type;   Width = w;  Height = h; Scale = s;
        }

        public void AddNodes(int x,int y,int s )
        {
            var center = new Vector2(x + s / 2f, y + s / 2f);
            _nodes.Add(new Node(center));
        }

        public void AddBones(int a, int b, Vector2 pivot)
                => _bones.Add(new Bone(_nodes[a], _nodes[b], Color.DarkGreen, pivot));
        public void AddTexture(ContentManager content)
        {
            _leafTextures = new Dictionary<char, Texture2D>();
            for (char c = 'A'; c <= 'O'; c++)
                _leafTextures[c] = content.Load<Texture2D>($"{TreeName}_{c}");
        }
    }    


    public class Herbium
    {
        
        List<Flora> plants;
    }


    // Data structures
    public class Node
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public readonly Vector2 RestPosition;
        public readonly float Mass;

        public Node(Vector2 pos, float mass = 2f)
        {
            Position = pos;
            RestPosition = pos;
            Velocity = Vector2.Zero;
            Mass = mass;
        }
    }
    public class Bone
    {
        public Node A, B;
        public readonly float RestLength;
        public readonly Color Color;
        public readonly Vector2 PivotLocal; // local pivot in texture space

        public Bone(Node a, Node b, Color color, Vector2 pivotLocal)
        {
            A = a; B = b;
            RestLength = Vector2.Distance(a.Position, b.Position);
            Color = color;
            PivotLocal = pivotLocal;
        }
    }
}
