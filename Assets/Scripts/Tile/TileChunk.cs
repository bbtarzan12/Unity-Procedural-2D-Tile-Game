using System;
using System.Collections.Generic;
using OptIn.Util;
using SimplexNoise;
using Unity.Mathematics;
using UnityEngine;

namespace OptIn.Tile
{
    [RequireComponent(typeof(TileMesh))]
    public class TileChunk : MonoBehaviour
    {
        int2 chunkPosition;
        bool meshDirty;
        bool lightDirty;

        TileManager tileManager;
        TileMesh mesh;
        TileLightTexture lightTexture;

        public bool Dirty => meshDirty || lightDirty;
        public int2 ChunkPosition => chunkPosition;

        void Awake()
        {
            mesh = GetComponent<TileMesh>();
            lightTexture = new GameObject("Light").AddComponent<TileLightTexture>();
        }

        public void Init(int2 position, TileManager manager, int2 chunkSize, int2 mapSize, Material tileMaterial, Material lightMaterial)
        {
            tileManager = manager;
            chunkPosition = position;
            mesh.Init(chunkSize, mapSize, chunkPosition, tileMaterial);
            lightTexture.Init(chunkSize, mapSize, chunkPosition, lightMaterial);

            int2 lightTextureTilePosition = TileUtil.TileToWorldTile(int2.zero, chunkPosition, chunkSize);
            Vector3 lightTexturePosition = new Vector3(lightTextureTilePosition.x, lightTextureTilePosition.y);
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

            mesh.UpdateMesh(tileManager.Tiles, tileManager.WaterDensities);
            meshDirty = false;
        }

        void UpdateLightTexture()
        {
            if (!lightDirty)
                return;

            lightTexture.UpdateTexture(tileManager.Lights);
            lightDirty = false;
        }

        public void SetMeshDirty()
        {
            meshDirty = true;
        }

        public void SetLightDirty()
        {
            lightDirty = true;
        }

    }
}