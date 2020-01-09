using System;
using System.Collections.Generic;
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
        bool meshDirty;
        bool lightDirty;

        TileMesh mesh;
        TileLightTexture lightTexture;

        public bool Dirty => meshDirty || lightDirty;
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
            
            meshDirty = true;
            lightDirty = true;
        }

        public void UpdateChunk()
        {
            UpdateMesh();
            UpdateLightTexture();
        }

        void UpdateMesh()
        {
            if (!meshDirty)
                return;
            
            mesh.UpdateMesh(tiles);
            meshDirty = false;
        }

        void UpdateLightTexture()
        {
            if(!lightDirty)
                return;
            
            lightTexture.UpdateTexture(lights);
            lightDirty = false;
        }

        public void SetTorchLight(Vector2Int tilePosition, int value)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
                return;

            int index = TileUtil.To1DIndex(tilePosition, chunkSize);

            if (lights[index].GetTorchLight() == value)
                return;

            lights[index].SetTorchLight(value);
            lightDirty = true;
        }

        public int GetTorchLight(Vector2Int tilePosition)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
                return 0;

            return lights[TileUtil.To1DIndex(tilePosition, chunkSize)].GetTorchLight();
        }
        
        public void SetSunLight(Vector2Int tilePosition, int value)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
                return;

            int index = TileUtil.To1DIndex(tilePosition, chunkSize);

            if (lights[index].GetSunLight() == value)
                return;

            lights[index].SetSunLight(value);
            lightDirty = true;
        }
        
        public int GetSunLight(Vector2Int tilePosition)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
                return 0;

            return lights[TileUtil.To1DIndex(tilePosition, chunkSize)].GetSunLight();
        }

        public bool SetTile(Vector2Int tilePosition, Tile tile)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
                return false;

            int index = TileUtil.To1DIndex(tilePosition, chunkSize);

            if (tiles[index].id == tile.id)
                return false;
            
            SetTorchLight(tilePosition, tile.emission);
            
            tiles[index] = tile;
            meshDirty = true;

            return true;
        }
        
        public bool GetTile(Vector2Int tilePosition, out Tile tile)
        {
            if (!TileUtil.BoundaryCheck(tilePosition, chunkSize))
            {
                tile = Tile.Empty;
                return false;
            }

            tile = tiles[TileUtil.To1DIndex(tilePosition, chunkSize)];
            return true;
        }
    }
}