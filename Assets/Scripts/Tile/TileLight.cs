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
        
        public static readonly int MaxSunLight = 15;
    }
}