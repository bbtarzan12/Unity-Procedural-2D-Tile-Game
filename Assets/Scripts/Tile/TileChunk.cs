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
        TileLight[] lights;
        Vector2Int chunkPosition;
        Vector2Int chunkSize;
        bool dirty;

        TileMesh mesh;
        TileLightTexture lightTexture;

        public bool Dirty => dirty;
        public Vector2Int ChunkPosition => chunkPosition;

        void Awake()
        {
            mesh = GetComponent<TileMesh>();
            lightTexture = new GameObject("Light").AddComponent<TileLightTexture>();
        }
        
        public void Init(Vector2Int position, Vector2Int size, Material tileMaterial, Material lightMaterial)
        {
            chunkPosition = position;
            chunkSize = size;
            tiles = new Tile[chunkSize.x * chunkSize.y];
            lights = new TileLight[chunkSize.x * chunkSize.y];
            mesh.Init(chunkSize, tileMaterial);
            lightTexture.Init(chunkSize, lightMaterial);
            
            Vector3 lightTexturePosition = transform.position;
            lightTexturePosition.z -= 10;
            lightTexture.transform.position = lightTexturePosition;
            
            dirty = true;
        }

        public void UpdateChunk()
        {
            UpdateMesh();
            UpdateLightTexture();
            dirty = false;
        }

        void UpdateMesh()
        {
            mesh.UpdateMesh(tiles);
        }

        void UpdateLightTexture()
        {
            lightTexture.UpdateTexture(lights);
        }

        public void SetSunLight(Vector2Int tilePosition, int value)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
                return;

            lights[TileUtil.To1DIndex(tilePosition, chunkSize)].SetSunLight(value);
            dirty = true;
        }
        
        public int GetSunLight(Vector2Int tilePosition)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
                return 0;

            return lights[TileUtil.To1DIndex(tilePosition, chunkSize)].GetSunLight();
        }

        public void SetTile(Vector2Int tilePosition, int type)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
                return;

            tiles[TileUtil.To1DIndex(tilePosition, chunkSize)].type = type;
            dirty = true;
        }
        
        public bool GetTile(Vector2Int tilePosition, out Tile tile)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
            {
                Debug.Log($"{tilePosition}");
                tile = Tile.Empty;
                return false;
            }

            tile = tiles[TileUtil.To1DIndex(tilePosition, chunkSize)];
            return true;
        }
    }
}