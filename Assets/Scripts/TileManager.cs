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
    Queue<Vector2Int> sunLightPropagationQueue = new Queue<Vector2Int>();
    Queue<Tuple<Vector2Int, int>> sunLightRemovalQueue = new Queue<Tuple<Vector2Int, int>>();

    Queue<Vector2Int> torchLightPropagationQueue = new Queue<Vector2Int>();
    Queue<Tuple<Vector2Int, int>> torchLightRemovalQueue = new Queue<Tuple<Vector2Int, int>>();

    static readonly Tile[] tiles =
    {
        Tile.Empty,
        new Tile{id = 1, color = new Color32(139, 192, 157, 255)},
        new Tile{id = 2, color = new Color32(255, 242, 161, 255), emission = 12}, 
    };

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
                    
                    SetTile(new Vector2Int(x, y), tiles[1]);
                }
            }
        }
    }
    
    void BuildSunLight()
    {
        for (int x = 0; x < mapSize.x; x++)
        {
            Vector2Int worldTilePosition = new Vector2Int(x, mapSize.y - 1);
            SetSunLight(worldTilePosition, TileLight.MaxSunLight);
            sunLightPropagationQueue.Enqueue(worldTilePosition);
        }
    }

    void Update()
    {
        UpdateChunks();
        SunLightPropagation();
        TorchLightPropagation();

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int worldTilePosition = TileUtil.WorldToWorldtile(mousePosition);
        if (Input.GetMouseButton(0))
        {
            SetTile(worldTilePosition, tiles[1]);
        }
        else if (Input.GetMouseButton(1))
        {
            SetTile(worldTilePosition, Tile.Empty);   
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            SetTile(worldTilePosition, tiles[2]);
        }
        
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

    void CheckTileToUpdateLight(Vector2Int worldTilePosition, Tile tile, Tile beforeTile)
    {
        if (tile.id == 0)
        {
            foreach (Vector2Int direction in TileUtil.Direction4)
            {
                Vector2Int neighborPosition = worldTilePosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborSunLight = GetSunLight(neighborPosition);

                if (neighborSunLight > 0)
                    sunLightPropagationQueue.Enqueue(neighborPosition);
            }
            
            torchLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, beforeTile.emission));
        }
        else
        {
            int sunLight = GetSunLight(worldTilePosition);
            SetSunLight(worldTilePosition, 0);
            sunLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, sunLight));

            if(tile.emission > 0)
                torchLightPropagationQueue.Enqueue(worldTilePosition);
        }
    }

    public void SunLightPropagation()
    {
        while (sunLightRemovalQueue.Count != 0)
        {
            (Vector2Int lightPosition, int sunLight) = sunLightRemovalQueue.Dequeue();

            foreach (Vector2Int direction in TileUtil.Direction4)
            {
                Vector2Int neighborPosition = lightPosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborSunLight = GetSunLight(neighborPosition);

                if (neighborSunLight != 0 && neighborSunLight < sunLight || direction == Vector2Int.down && sunLight == TileLight.MaxSunLight)
                {
                    SetSunLight(neighborPosition, 0);
                    sunLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(neighborPosition, neighborSunLight));
                }
                else if (neighborSunLight >= sunLight)
                {
                    sunLightPropagationQueue.Enqueue(neighborPosition);
                }
            }
        }

        while (sunLightPropagationQueue.Count != 0)
        {
            Vector2Int lightPosition = sunLightPropagationQueue.Dequeue();
            int sunLight = GetSunLight(lightPosition);

            if (sunLight <= 0)
                continue;

            foreach (Vector2Int direction in TileUtil.Direction4)
            {
                Vector2Int neighborPosition = lightPosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborSunLight = GetSunLight(neighborPosition);

                int resultSunLight = sunLight - 1;

                bool isOpacity = GetTile(neighborPosition, out Tile neighborTile) && neighborTile.id != 0;

                if (isOpacity)
                    resultSunLight -= 2;

                if (direction == Vector2Int.down && !isOpacity && sunLight == TileLight.MaxSunLight)
                {
                    SetSunLight(neighborPosition, TileLight.MaxSunLight);
                    sunLightPropagationQueue.Enqueue(neighborPosition);
                }
                else if(neighborSunLight < resultSunLight)
                {
                    SetSunLight(neighborPosition, resultSunLight);
                    sunLightPropagationQueue.Enqueue(neighborPosition);
                }
            }

        }
    }

    public void TorchLightPropagation()
    {
        while (torchLightRemovalQueue.Count != 0)
        {
            (Vector2Int lightPosition, int torchLight) = torchLightRemovalQueue.Dequeue();

            foreach (Vector2Int direction in TileUtil.Direction4)
            {
                Vector2Int neighborPosition = lightPosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborTorchLight = GetTorchLight(neighborPosition);

                if (neighborTorchLight != 0 && neighborTorchLight < torchLight)
                {
                    SetTorchLight(neighborPosition, 0);
                    torchLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(neighborPosition, neighborTorchLight));
                }
                else if (neighborTorchLight >= torchLight)
                {
                    torchLightPropagationQueue.Enqueue(neighborPosition);
                }
            }
        }

        while (torchLightPropagationQueue.Count != 0)
        {
            Vector2Int lightPosition = torchLightPropagationQueue.Dequeue();
            int torchLight = GetTorchLight(lightPosition);

            if (torchLight <= 0)
                continue;

            foreach (Vector2Int direction in TileUtil.Direction4)
            {
                Vector2Int neighborPosition = lightPosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborTorchLight = GetTorchLight(neighborPosition);

                int resultTorchLight = torchLight - 1;

                bool isOpacity = GetTile(neighborPosition, out Tile neighborTile) && neighborTile.id != 0;

                if (isOpacity)
                    resultTorchLight -= 2;

                if (neighborTorchLight >= resultTorchLight) 
                    continue;
                
                SetTorchLight(neighborPosition, resultTorchLight);
                torchLightPropagationQueue.Enqueue(neighborPosition);
            }
        }
    }

    public void SetTorchLight(Vector2Int worldTilePosition, int value)
    {
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        Vector2Int tilePosition = TileUtil.WorldTileToTile(worldTilePosition, chunkPosition, chunkSize);
        
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            chunk.SetTorchLight(tilePosition, value);
        }
        else if(TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            newChunk.SetTorchLight(tilePosition, value);
        }
    }

    public int GetTorchLight(Vector2Int worldTilePosition)
    {
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        Vector2Int tilePosition = TileUtil.WorldTileToTile(worldTilePosition, chunkPosition, chunkSize);
        
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            return chunk.GetTorchLight(tilePosition);
        }
        else if(TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            return newChunk.GetTorchLight(tilePosition);
        }

        return 0;
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

    public void SetTile(Vector2Int worldTilePosition, Tile tile)
    {
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        Vector2Int tilePosition = TileUtil.WorldTileToTile(worldTilePosition, chunkPosition, chunkSize);

        Tile beforeTile;
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            chunk.GetTile(tilePosition, out beforeTile);
            if (chunk.SetTile(tilePosition, tile))
            {
                CheckTileToUpdateLight(worldTilePosition, tile, beforeTile);
            }
        }
        else if(TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            newChunk.GetTile(tilePosition, out beforeTile);
            if (newChunk.SetTile(tilePosition, tile))
            {
                CheckTileToUpdateLight(worldTilePosition, tile, beforeTile);
            }
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
