using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using OptIn.Tile;
using OptIn.Util;
using SimplexNoise;
using UnityEditor.Tilemaps;
using UnityEngine;

public class TileManager : MonoBehaviour
{
    [SerializeField] Vector2Int mapSize;
    [SerializeField] Vector2Int chunkSize;
    [SerializeField] int numUpdateChunkInFrame;
    [SerializeField] Material tileMaterial;

    Dictionary<Vector2Int, TileChunk> chunks = new Dictionary<Vector2Int, TileChunk>();

    void Start()
    {
        int halfHeight = mapSize.y / 2;
        for (int x = 0; x < mapSize.x; x++)
        {
            for (int y = 0; y < mapSize.y; y++)
            {
                float density = halfHeight - y;
                density += Noise.CalcPixel1DFractal(x, 0.03f, 3) * 30.0f;

                float noise = Noise.CalcPixel2DFractal(x, y, 0.01f, 3);

                if (density > 0)
                {
                    if(noise <= 0.3)
                        continue;
                    
                    SetTile(new Vector2Int(x, y), 1);
                }
            }
        }
    }

    void Update()
    {
        UpdateChunks();
    }

    void UpdateChunks()
    {
        int numChunk = 0;
        foreach (var pair in chunks)
        {
            if (numChunk >= numUpdateChunkInFrame)
                break;
            
            if(!pair.Value.Dirty)
                continue;
            
            pair.Value.UpdateChunk();
            numChunk++;
        }
    }

    void SetTile(Vector2Int worldTilePosition, int type)
    {
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        Vector2Int tilePosition = TileUtil.WorldTileToTile(worldTilePosition, chunkPosition, chunkSize);
        
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            chunk.SetTile(tilePosition, type);
        }
        else
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            newChunk.SetTile(tilePosition, type);
        }
    }
    
    TileChunk GenerateChunk(Vector2Int chunkPosition)
    {
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            return chunk;
        }
        else
        {
            TileChunk newChunk = new GameObject(chunkPosition.ToString()).AddComponent<TileChunk>();
            newChunk.transform.position = TileUtil.ChunkToWorld(chunkPosition, chunkSize);
            newChunk.Init(chunkPosition, chunkSize, tileMaterial);
            chunks.Add(chunkPosition, newChunk);
            
            return newChunk;
        }
    }

}
