using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace OptIn.Tile
{
    [Serializable]
    public struct Tile
    {
        public int id;
        public LightEmission emission;
        public int attenuation;
        public Color32 color;
        public bool isSolid;
    }
}