using System;
using System.Collections;
using System.Collections.Generic;
using OptIn.Tile;
using OptIn.Util;
using SimplexNoise;
using UnityEngine;
using LightType = OptIn.Tile.LightType;

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

    Queue<Vector2Int> torchRedLightPropagationQueue = new Queue<Vector2Int>();
    Queue<Vector2Int> torchGreenLightPropagationQueue = new Queue<Vector2Int>();
    Queue<Vector2Int> torchBlueLightPropagationQueue = new Queue<Vector2Int>();

    Queue<Tuple<Vector2Int, int>> torchRedLightRemovalQueue = new Queue<Tuple<Vector2Int, int>>();
    Queue<Tuple<Vector2Int, int>> torchGreenLightRemovalQueue = new Queue<Tuple<Vector2Int, int>>();
    Queue<Tuple<Vector2Int, int>> torchBlueLightRemovalQueue = new Queue<Tuple<Vector2Int, int>>();

    static readonly Tile[] tiles =
    {
        Tile.Empty,
        new Tile{id = 1, color = new Color32(139, 192, 157, 255), attenuation = 50},
        new Tile{id = 2, color = new Color32(255, 242, 161, 255), emission = new LightEmission{r = TileLight.MaxTorchLight, g = TileLight.MaxTorchLight, b = 0}}, 
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
            SetLight(worldTilePosition, TileLight.MaxSunLight, LightType.S);
            sunLightPropagationQueue.Enqueue(worldTilePosition);
        }
    }

    void Update()
    {
        UpdateChunks();
        SunLightPropagation();
        TorchLightPropagation(ref torchRedLightPropagationQueue, ref torchRedLightRemovalQueue, LightType.R);
        TorchLightPropagation(ref torchGreenLightPropagationQueue, ref torchGreenLightRemovalQueue, LightType.G);
        TorchLightPropagation(ref torchBlueLightPropagationQueue, ref torchBlueLightRemovalQueue, LightType.B);

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

    void CheckTileToUpdateLight(Vector2Int worldTilePosition, Tile tile, LightEmission beforeEmission)
    {
        if (tile.id == 0)
        {
            foreach (Vector2Int direction in TileUtil.Direction4)
            {
                Vector2Int neighborPosition = worldTilePosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborSunLight = GetLight(neighborPosition, LightType.S);

                if (neighborSunLight > 0)
                    sunLightPropagationQueue.Enqueue(neighborPosition);
            }
        }
        else
        {
            int sunLight = GetLight(worldTilePosition, LightType.S);
            SetLight(worldTilePosition, 0, LightType.S);
            sunLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, sunLight));
        }
        
        if (tile.emission.r > 0)
            torchRedLightPropagationQueue.Enqueue(worldTilePosition);

        if (tile.emission.g > 0)
            torchGreenLightPropagationQueue.Enqueue(worldTilePosition);

        if (tile.emission.b > 0)
            torchBlueLightPropagationQueue.Enqueue(worldTilePosition);

        if (beforeEmission.r > 0) 
            torchRedLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, beforeEmission.r));

        if(beforeEmission.g > 0)
            torchGreenLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, beforeEmission.g));
            
        if(beforeEmission.b > 0)
            torchBlueLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, beforeEmission.b));
        
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

                int neighborSunLight = GetLight(neighborPosition, LightType.S);

                if (neighborSunLight != 0 && neighborSunLight < sunLight || direction == Vector2Int.down && sunLight == TileLight.MaxSunLight)
                {
                    SetLight(neighborPosition, 0, LightType.S);
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
            int sunLight = GetLight(lightPosition, LightType.S);

            if (sunLight <= 0)
                continue;

            foreach (Vector2Int direction in TileUtil.Direction4)
            {
                Vector2Int neighborPosition = lightPosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborSunLight = GetLight(neighborPosition, LightType.S);

                int resultSunLight = sunLight - TileLight.SunLightAttenuation;

                bool isOpacity = GetTile(neighborPosition, out Tile neighborTile) && neighborTile.id != 0;

                if (isOpacity)
                    resultSunLight -= neighborTile.attenuation;

                if (direction == Vector2Int.down && !isOpacity && sunLight == TileLight.MaxSunLight)
                {
                    SetLight(neighborPosition, TileLight.MaxSunLight, LightType.S);
                    sunLightPropagationQueue.Enqueue(neighborPosition);
                }
                else if(neighborSunLight < resultSunLight)
                {
                    SetLight(neighborPosition, resultSunLight, LightType.S);
                    sunLightPropagationQueue.Enqueue(neighborPosition);
                }
            }

        }
    }

    public void TorchLightPropagation(ref Queue<Vector2Int> propagationQueue, ref Queue<Tuple<Vector2Int, int>> removalQueue, LightType lightType)
    {
        while (removalQueue.Count != 0)
        {
            (Vector2Int lightPosition, int torchLight) = removalQueue.Dequeue();

            foreach (Vector2Int direction in TileUtil.Direction4)
            {
                Vector2Int neighborPosition = lightPosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborTorchLight = GetLight(neighborPosition, lightType);

                if (neighborTorchLight != 0 && neighborTorchLight < torchLight)
                {
                    SetLight(neighborPosition, 0, lightType);
                    removalQueue.Enqueue(new Tuple<Vector2Int, int>(neighborPosition, neighborTorchLight));
                }
                else if (neighborTorchLight >= torchLight)
                {
                    propagationQueue.Enqueue(neighborPosition);
                }
            }
        }

        while (propagationQueue.Count != 0)
        {
            Vector2Int lightPosition = propagationQueue.Dequeue();
            int torchLight = GetLight(lightPosition, lightType);

            if (torchLight <= 0)
                continue;

            foreach (Vector2Int direction in TileUtil.Direction4)
            {
                Vector2Int neighborPosition = lightPosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborTorchLight = GetLight(neighborPosition, lightType);

                int resultTorchLight = torchLight - TileLight.SunLightAttenuation;

                bool isOpacity = GetTile(neighborPosition, out Tile neighborTile) && neighborTile.id != 0;

                if (isOpacity)
                    resultTorchLight -= neighborTile.attenuation;

                if (neighborTorchLight >= resultTorchLight)
                    continue;

                SetLight(neighborPosition, resultTorchLight, lightType);
                propagationQueue.Enqueue(neighborPosition);
            }
        }
    }

    public void SetLight(Vector2Int worldTilePosition, int value, LightType type)
    {
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        Vector2Int tilePosition = TileUtil.WorldTileToTile(worldTilePosition, chunkPosition, chunkSize);
        
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            chunk.SetLight(tilePosition, value, type);
        }
        else if(TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            newChunk.SetLight(tilePosition, value, type);
        } 
    }

    public int GetLight(Vector2Int worldTilePosition, LightType type)
    {
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        Vector2Int tilePosition = TileUtil.WorldTileToTile(worldTilePosition, chunkPosition, chunkSize);
        
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            return chunk.GetLight(tilePosition, type);
        }
        else if(TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            return newChunk.GetLight(tilePosition, type);
        }

        return 0;
    }

    public void SetTile(Vector2Int worldTilePosition, Tile tile)
    {
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        Vector2Int tilePosition = TileUtil.WorldTileToTile(worldTilePosition, chunkPosition, chunkSize);

        LightEmission beforeEmission;
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            beforeEmission = chunk.GetEmission(tilePosition);
            if (chunk.SetTile(tilePosition, tile))
            {
                CheckTileToUpdateLight(worldTilePosition, tile, beforeEmission);
            }
        }
        else if (TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            beforeEmission = newChunk.GetEmission(tilePosition);
            if (newChunk.SetTile(tilePosition, tile))
            {
                CheckTileToUpdateLight(worldTilePosition, tile, beforeEmission);
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
