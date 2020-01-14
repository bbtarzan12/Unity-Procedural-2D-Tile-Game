using Unity.Mathematics;
using UnityEngine;

namespace OptIn.Util
{
    public static class TileUtil
    {

        public static int To1DIndex(int2 index, int2 size)
        {
            return index.x + index.y * size.x;
        }

        public static int2 To2DIndex(int index, int2 size)
        {
            return new int2
            {
                x = index % size.x,
                y = index / size.x
            };
        }

        public static Vector2 ChunkToWorld(int2 chunkPosition, int2 size)
        {
            return new Vector2
            {
                x = chunkPosition.x * size.x,
                y = chunkPosition.y * size.y
            };
        }

        public static int2 WorldTileToChunk(int2 worldTilePosition, int2 size)
        {
            return new int2
            {               
                x = Mathf.FloorToInt(worldTilePosition.x / (float)size.x),
                y = Mathf.FloorToInt(worldTilePosition.y / (float)size.y)
            };
        }
        
        public static int2 WorldToWorldtile(Vector3 worldPosition)
        {
            return new int2
            {
                x = Mathf.FloorToInt(worldPosition.x),
                y = Mathf.FloorToInt(worldPosition.y)
            };
        }

        public static int2 WorldTileToTile(int2 worldTilePosition, int2 chunkPosition, int2 size)
        {
            return worldTilePosition - chunkPosition * size;
        }

        public static int2 TileToWorldTile(int2 tilePosition, int2 chunkPosition, int2 size)
        {
            return tilePosition + chunkPosition * size;
        }

        public static bool BoundaryCheck(int2 tilePosition, int2 size)
        {
            return !(tilePosition.x < 0 || tilePosition.y < 0 || tilePosition.x >= size.x || tilePosition.y >= size.y);
        }

        public static bool BoundaryCheck(int tileIndex, int2 size)
        {
            return !(tileIndex < 0 || tileIndex >= size.x * size.y);
        }

        public static bool BoundaryCheck(int2 worldTilePosition, int2 chunkPosition, int2 size)
        {
            return BoundaryCheck(WorldTileToTile(worldTilePosition, chunkPosition, size), size);
        }

        public static readonly int2[] Direction4 =
        {
            new int2(0, -1),
            new int2(0, 1),
            new int2(1, 0),
            new int2(-1, 0)
        };
        
        public static readonly int2[] Direction8 =
        {
            new int2(1, 0),
            new int2(-1, 0),
            new int2(0, 1),
            new int2(0, -1),
            new int2(1, 1),
            new int2(-1, -1),
            new int2(-1, 1),
            new int2(1, -1)
        };
        
        public static readonly int2 Up = new int2(0, 1);
        public static readonly int2 Down = new int2(0, -1);
        public static readonly int2 Left = new int2(-1, 0);
        public static readonly int2 Right = new int2(1, 0);

    }
}