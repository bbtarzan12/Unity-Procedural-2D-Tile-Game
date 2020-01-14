using UnityEngine;

namespace OptIn.Tile
{
    public struct Tile
    {
        public int id;
        public LightEmission emission;
        public int attenuation;
        public Color32 color;
        public bool isSolid;
    }
}