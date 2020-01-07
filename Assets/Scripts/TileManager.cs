using System;
using System.Collections;
using System.Collections.Generic;
using OptIn.Tile;
using OptIn.Util;
using SimplexNoise;
using UnityEngine;

public class TileManager : MonoBehaviour
{
    [SerializeField] Vector2Int mapSize;
    [SerializeField] Vector2Int chunkSize;
    [SerializeField] int numUpdateChunkInFrame;
    [SerializeField] Material tileMaterial;
    [SerializeField] Material lightMaterial;

    Dictionary<Vector2Int, TileChunk> chunks = new Dictionary<Vector2Int, TileChunk>();

    void Start()
    {
        GenerateTerrain();
        BuildSunLight();
        
        
    }

    void GenerateTerrain()
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
    
    void BuildSunLight()
    {
        Queue<Vector2Int> sunLightQueue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        
        for (int x = 0; x < mapSize.x; x++)
        {
            Vector2Int worldTilePosition = new Vector2Int(x, mapSize.y - 1);
        
            if(GetTile(worldTilePosition, out Tile tile))
            {
                if(tile.type == 1)
                    continue;
            }
            else
            {
                continue;
            }
            
            SetSunLight(worldTilePosition, TileLight.MaxSunLight);
            sunLightQueue.Enqueue(worldTilePosition);
        }
        
        while (sunLightQueue.Count != 0)
        {
            Vector2Int lightPosition = sunLightQueue.Dequeue();
            int sunLight = GetSunLight(lightPosition);
            
            if(sunLight <= 0)
                continue;
            
            visited.Add(lightPosition);
        
            foreach (Vector2Int direction in TileUtil.Direction4)
            {

                Vector2Int neighborPosition = lightPosition + direction;
                
                if(!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;
                
                int neighborSunLight = GetSunLight(neighborPosition);

                int resultSunLight = sunLight - 1;

                bool isOpacity = GetTile(neighborPosition, out Tile neighborTile) && neighborTile.type != 0;

                if (isOpacity)
                    resultSunLight -= 2;


                if(neighborSunLight >= resultSunLight)
                    continue;
                
                if (direction == Vector2Int.down && !isOpacity && sunLight == TileLight.MaxSunLight)
                {
                    SetSunLight(neighborPosition, TileLight.MaxSunLight);
                    sunLightQueue.Enqueue(neighborPosition);
                }
                else
                {
                    SetSunLight(neighborPosition, resultSunLight);
                    sunLightQueue.Enqueue(neighborPosition);
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

    public void SetSunLight(Vector2Int worldTilePosition, int value)
    {
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        Vector2Int tilePosition = TileUtil.WorldTileToTile(worldTilePosition, chunkPosition, chunkSize);
        
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            chunk.SetSunLight(tilePosition, value);
        }
        else if(TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            newChunk.SetSunLight(tilePosition, value);
        }
    }

    public int GetSunLight(Vector2Int worldTilePosition)
    {
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        Vector2Int tilePosition = TileUtil.WorldTileToTile(worldTilePosition, chunkPosition, chunkSize);
        
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            return chunk.GetSunLight(tilePosition);
        }
        else if(TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            return newChunk.GetSunLight(tilePosition);
        }

        return 0;
    }

    public void SetTile(Vector2Int worldTilePosition, int type)
    {
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        Vector2Int tilePosition = TileUtil.WorldTileToTile(worldTilePosition, chunkPosition, chunkSize);
        
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            chunk.SetTile(tilePosition, type);
        }
        else if(TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            newChunk.SetTile(tilePosition, type);
        }
    }

    public bool GetTile(Vector2Int worldTilePosition, out Tile tile)
    {
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        Vector2Int tilePosition = TileUtil.WorldTileToTile(worldTilePosition, chunkPosition, chunkSize);

        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            return chunk.GetTile(tilePosition, out tile);
        }
        else if(TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            return newChunk.GetTile(tilePosition, out tile);
        }

        tile = Tile.Empty;
        return false;
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
            newChunk.Init(chunkPosition, chunkSize, tileMaterial, lightMaterial);
            chunks.Add(chunkPosition, newChunk);
            
            return newChunk;
        }
    }
}
