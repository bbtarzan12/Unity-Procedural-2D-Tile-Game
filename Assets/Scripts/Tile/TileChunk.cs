using System;
using OptIn.Util;
using SimplexNoise;
using UnityEngine;

namespace OptIn.Tile
{
    [RequireComponent(typeof(TileMesh))]
    public class TileChunk : MonoBehaviour
    {
        Tile[] tiles;
        Vector2Int chunkPosition;
        Vector2Int chunkSize;
        bool dirty;

        TileMesh mesh;

        public bool Dirty => dirty;
        public Vector2Int ChunkPosition => chunkPosition;

        void Awake()
        {
            mesh = GetComponent<TileMesh>();
        }
        
        public void Init(Vector2Int position, Vector2Int size, Material tileMaterial)
        {
            chunkPosition = position;
            chunkSize = size;
            tiles = new Tile[chunkSize.x * chunkSize.y];
            mesh.Init(chunkSize, tileMaterial);
            
            dirty = true;
        }

        public void UpdateChunk()
        {
            UpdateMesh();
            dirty = false;
        }

        void UpdateMesh()
        {
            mesh.UpdateMesh(tiles);
        }

        public void SetTile(Vector2Int tilePosition, int type)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
                return;

            tiles[TileUtil.To1DIndex(tilePosition, chunkSize)].type = type;
            dirty = true;
        }
        
        public bool GetTile(Vector2Int tilePosition, ref Tile tile)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
                return false;

            tile = tiles[TileUtil.To1DIndex(tilePosition, chunkSize)];
            return true;
        }
    }
}