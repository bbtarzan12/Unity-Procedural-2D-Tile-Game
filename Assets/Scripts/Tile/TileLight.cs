namespace OptIn.Tile
{
    public enum LightType {S, R, G ,B}

    public struct LightEmission
    {
        public byte r;
        public byte g;
        public byte b;

        public static readonly LightEmission Zero = new LightEmission {r = 0, g = 0, b = 0};
    }
    
    public struct TileLight
    {
        public static readonly int MaxSunLight = 15;
        public static readonly int MaxTorchLight = 15;
        
        short light; // SSSS RRRR GGGG BBBB, S : SunLight
        
        public int GetLight(LightType type)
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
            switch (type)
            {
                case LightType.S: 
                    SetSunLight(value);
                    break;
                case LightType.R:
                    SetRedLight(value);
                    break;
                case LightType.G:
                    SetGreenLight(value);
                    break;
                case LightType.B:
                    SetBlueLight(value);
                    break;
            }
        }

        public LightEmission GetEmission()
        {
            return new LightEmission{r = (byte)GetRedLight(), g = (byte)GetGreenLight(), b = (byte)GetBlueLight()};
        }

        public int GetSunLight()
        {
            return (light >> 12) & 0xF;
        }

        public void SetSunLight(int value)
        {
            light = (short)((light & 0x0FFF) | (value << 12));
        }
        
        public int GetRedLight()
        {
            return (light >> 8) & 0xF;
        }

        public void SetRedLight(int value)
        {
            light = (short) ((light & 0xF0FF) | (value << 8));
        }

        public int GetGreenLight()
        {
            return (light >> 4) & 0xF;
        }

        public void SetGreenLight(int value)
        {
            light = (short) ((light & 0xFF0F) | (value << 4));
        }

        public int GetBlueLight()
        {
            return light & 0xF;
        }

        public void SetBlueLight(int value)
        {
            light = (short) ((light & 0xFFF0) | value);
        }
    }
}