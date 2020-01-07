namespace OptIn.Tile
{
    public struct Tile
    {
        public int type;
        
        public static readonly Tile Empty = new Tile{type = 0};
    }
}