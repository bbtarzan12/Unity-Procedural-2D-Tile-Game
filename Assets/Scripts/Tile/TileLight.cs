using System;
using System.Runtime.InteropServices;

namespace OptIn.Tile
{
    public enum LightType {S, R, G ,B}

    [Serializable]
    public struct LightEmission
    {
        public byte r;
        public byte g;
        public byte b;
    }
    
    [Serializable]
    public struct TileLight
    {
        public const byte MaxSunLight = 255;
        public const byte MaxTorchLight = 255;
        public const int SunLightAttenuation = 10;

        byte sunLight;
        LightEmission emission;
                
        public byte GetLight(LightType type)
        {
            switch (type)
            {
                case LightType.S: return GetSunLight();
                case LightType.R: return GetRedLight();
                case LightType.G: return GetGreenLight();
                case LightType.B: return GetBlueLight();
            }
            return 0;
        }
        
        public void SetLight(int value, LightType type)
        {
            if (value < 0)
                value = 0;
            
            switch (type)
            {
                case LightType.S: 
                    SetSunLight((byte) value);
                    break;
                case LightType.R:
                    SetRedLight((byte) value);
                    break;
                case LightType.G:
                    SetGreenLight((byte) value);
                    break;
                case LightType.B:
                    SetBlueLight((byte) value);
                    break;
            }
        }

        public LightEmission GetEmission()
        {
            return new LightEmission{r = GetRedLight(), g = GetGreenLight(), b = GetBlueLight()};
        }

        public byte GetSunLight()
        {
            return sunLight;
        }

        public void SetSunLight(byte value)
        {
            sunLight = value;
        }
        
        public byte GetRedLight()
        {
            return emission.r;
        }

        public void SetRedLight(byte value)
        {
            emission.r = value;
        }

        public byte GetGreenLight()
        {
            return emission.g;
        }

        public void SetGreenLight(byte value)
        {
            emission.g = value;
        }

        public byte GetBlueLight()
        {
            return emission.b;
        }

        public void SetBlueLight(byte value)
        {
            emission.b = value;
        }
    }
}