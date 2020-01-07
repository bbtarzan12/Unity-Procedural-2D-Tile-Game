using UnityEngine;

namespace OptIn.Util
{
    public static class TileUtil
    {

        public static int To1DIndex(Vector2Int index, Vector2Int size)
        {
            return index.x + index.y * size.x;
        }

        public static Vector2Int To2DIndex(int index, Vector2Int size)
        {
            return new Vector2Int
            {
                x = index % size.x,
                y = index / size.x
            };
        }

        public static Vector2 ChunkToWorld(Vector2Int chunkPosition, Vector2Int size)
        {
            return new Vector2
            {
                x = chunkPosition.x * size.x,
                y = chunkPosition.y * size.y
            };
        }

        public static Vector2Int WorldTileToChunk(Vector2Int worldTilePosition, Vector2Int size)
        {
            return new Vector2Int
            {               
                x = Mathf.FloorToInt(worldTilePosition.x / (float)size.x),
                y = Mathf.FloorToInt(worldTilePosition.y / (float)size.y)
            };
        }

        public static bool IsEdgeTile(Tile.Tile[] tiles, Vector2Int tilePosition, Vector2Int size)
        {
            bool isEdge = false;
            foreach (var direction in Direction4)
            {
                Vector2Int neighborPosition = tilePosition + direction;
                if (BoundaryCheck(neighborPosition, size))
                {
                    int index = To1DIndex(neighborPosition, size);
                    if (tiles[index].type == 0)
                    {
                        isEdge = true;
                        break;
                    }
                }
                else
                {
                    isEdge = true;
                    break;
                }
            }

            return isEdge;
        }

        public static Vector2Int WorldTileToTile(Vector2Int worldTilePosition, Vector2Int chunkPosition, Vector2Int size)
        {
            return worldTilePosition - chunkPosition * size;
        }

        public static bool BoundaryCheck(Vector2Int tilePosition, Vector2Int size)
        {
            return !(tilePosition.x < 0 || tilePosition.y < 0 || tilePosition.x >= size.x || tilePosition.y >= size.y);
        }

        public static readonly Vector2Int[] Direction4 =
        {
            new Vector2Int(0, -1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
        };

    }
}