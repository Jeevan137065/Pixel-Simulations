using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Pixel_Simulations
{
    public static class RigData
    {
        // Node indices
        public const int Head = 0;
        public const int Neck = 1;
        public const int ChestR = 2;
        public const int ChestL = 3;
        public const int ShoulderR = 4;
        public const int ShoulderL = 5;
        public const int ArmR = 6;
        public const int ArmL = 7;
        public const int COG = 8;
        public const int HandR = 9;
        public const int HandL = 10;
        public const int ThighR = 11;
        public const int ThighL = 12;
        public const int KneeR = 13;
        public const int KneeL = 14;
        public const int FootR = 15;
        public const int FootL = 16;

        // Create nodes with bind centers, sizes, masses, and colors
        public static List<Rig.Node> CreateNodes()
        {
            var nodes = new List<Rig.Node>();
            void Add(int x, int y, int size, float mass, Color color)
            {
                var center = new Vector2(x + size * 0.5f, y + size * 0.5f);
                nodes.Add(new Rig.Node(center, color, size, mass));
            }

            Add(14, 5, 2, 2f, Color.Red);
            Add(14, 12, 2, 1f, Color.Orange);
            Add(12, 17, 1, 2f, Color.Yellow);
            Add(17, 17, 1, 2f, Color.Yellow);
            Add(8, 16, 2, 1f, Color.Green);
            Add(20, 16, 2, 1f, Color.Green);
            Add(5, 23, 2, 0.5f, Color.Cyan);
            Add(23, 23, 2, 0.5f, Color.Cyan);
            Add(14, 24, 2, 5f, Color.Blue);
            Add(4, 32, 2, 0.3f, Color.Purple);
            Add(24, 32, 2, 0.3f, Color.Purple);
            Add(10, 30, 2, 2f, Color.Magenta);
            Add(18, 30, 2, 2f, Color.Magenta);
            Add(9, 38, 2, 1f, Color.Brown);
            Add(19, 38, 2, 1f, Color.Brown);
            Add(7, 47, 3, 3f, Color.Black);
            Add(20, 47, 3, 3f, Color.Black);

            return nodes;
        }

        // Create bones connecting nodes with rest lengths and stiffness
        public static List<Rig.Bone> CreateBones(List<Rig.Node> nodes)
        {
            var bones = new List<Rig.Bone>();
            void Conn(int a, int b, float k, Color color)
            {
                bones.Add(new Rig.Bone(nodes[a], nodes[b], k, color));
            }

            Conn(Head, Neck, 0.9f, Color.LightCoral);
            Conn(Neck, ChestR, 0.8f, Color.LightYellow);
            Conn(Neck, ChestL, 0.8f, Color.LightYellow);
            Conn(ChestR, ShoulderR, 0.7f, Color.LightGreen);
            Conn(ChestL, ShoulderL, 0.7f, Color.LightGreen);
            Conn(ShoulderR, ArmR, 0.5f, Color.Cyan);
            Conn(ShoulderL, ArmL, 0.5f, Color.Cyan);
            Conn(ArmR, HandR, 0.4f, Color.Purple);
            Conn(ArmL, HandL, 0.4f, Color.Purple);
            Conn(Neck, COG, 1.2f, Color.Blue);
            Conn(COG, ThighR, 0.6f, Color.Magenta);
            Conn(COG, ThighL, 0.6f, Color.Magenta);
            Conn(ThighR, KneeR, 0.5f, Color.Brown);
            Conn(ThighL, KneeL, 0.5f, Color.Brown);
            Conn(KneeR, FootR, 0.6f, Color.Black);
            Conn(KneeL, FootL, 0.6f, Color.Black);

            return bones;
        }

        // Feet are anchored
        public static bool IsFoot(int idx) => idx == FootR || idx == FootL;
    }

    public static class TreeRigData
    {
        // Node indices a–p
        public const int A = 0, B = 1, C = 2, D = 3, E = 4, F = 5, G = 6, H = 7,
                         I = 8, J = 9, K = 10, L = 11, M = 12, N = 13, O = 14, P = 15;

        public static List<Rig.Node> CreateTreeNodes()
        {
            var n = new List<Rig.Node>();
            void Add(int x, int y, int s)
            {
                var center = new Vector2(x + s / 2f, y + s / 2f);
                n.Add(new Rig.Node(center, Color.Brown, s, mass: 1f));
            }
            Add(62, 121, 4);  // a
            Add(65, 87, 3);  // b
            Add(50, 62, 2);  // c
            Add(72, 72, 3);  // d
            Add(80, 58, 2);  // e
            Add(72, 68, 2);  // f
            Add(65, 49, 2);  // g
            Add(76, 60, 2);  // h
            Add(55, 41, 2);  // i
            Add(65, 39, 1);  // j
            Add(75, 36, 1);  // k
            Add(37, 56, 1);  // l
            Add(47, 41, 2);  // m
            Add(25, 29, 1);  // n
            Add(46, 33, 2);  // o
            Add(54, 32, 1);  // p
            return n;
        }

        public static List<Rig.Bone> CreateTreeBones(List<Rig.Node> nodes)
        {
            var b = new List<Rig.Bone>();
            void Conn(int a, int c, float k)
                => b.Add(new Rig.Bone(nodes[a], nodes[c], k, Color.DarkGreen));
            Conn(A, B, 0.7f);  // AB
            Conn(B, C, 0.6f);  // BC
            Conn(B, D, 0.6f);  // BD
            Conn(D, F, 0.5f);  // DF
            Conn(D, E, 0.5f);  // DE
            Conn(F, H, 0.5f);  // FH
            Conn(F, G, 0.5f);  // FG
            Conn(G, J, 0.4f);  // GJ
            Conn(G, I, 0.4f);  // GI
            Conn(H, K, 0.4f);  // HK
            Conn(C, M, 0.5f);  // CM
            Conn(C, L, 0.5f);  // CL
            Conn(M, N, 0.4f);  // MN
            Conn(M, P, 0.4f);  // MP
            Conn(M, O, 0.4f);  // MO
            return b;
        }
    }

}
