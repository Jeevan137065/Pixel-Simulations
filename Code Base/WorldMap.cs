using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Timers;
using Pixel_Simulations.Code_Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pixel_Simulations
{
    

    public enum TileType { Grass, Dirt, Tilled }

    public struct Tile
    {
        public TileType Type;
        public int Variant;
        public Rectangle SourceRect;
    }

}