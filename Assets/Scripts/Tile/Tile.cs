using UnityEngine;

namespace OptIn.Tile
{
    public struct Tile
    {
        public int id;
        public LightEmission emission;
        public Color32 color;
        
        public static readonly Tile Empty = new Tile{id = 0};
    }
}