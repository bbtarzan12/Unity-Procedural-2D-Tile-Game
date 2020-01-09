namespace OptIn.Tile
{
    public struct TileLight
    {
        sbyte light;

        public int GetSunLight()
        {
            return (light >> 4) & 0xF;
        }

        public void SetSunLight(int value)
        {
            light = (sbyte)((light & 0xF) | (value << 4));
        }

        public int GetTorchLight()
        {
            return light & 0xF;
        }

        public void SetTorchLight(int value)
        {
            light = (sbyte) ((light & 0xF0) | value);
        }

        public static readonly int MaxSunLight = 15;
        public static readonly int MaxTorchLight = 15;
    }
}