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
    Tile[] tiles;
    TileLight[] lights;
    
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

    public Tile[] Tiles => tiles;
    public TileLight[] Lights => lights;

    static readonly Tile[] testTiles =
    {
        Tile.Empty,
        new Tile{id = 1, color = new Color32(139, 192, 157, 255), attenuation = 50},
        new Tile{id = 2, color = new Color32(255, 242, 161, 255), emission = new LightEmission{r = TileLight.MaxTorchLight, g = TileLight.MaxTorchLight, b = 0}}, 
    };

    void Awake()
    {
        tiles = new Tile[mapSize.x * mapSize.y];
        lights = new TileLight[mapSize.x * mapSize.y];
    }
    
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
                    
                    SetTile(new Vector2Int(x, y), testTiles[1]);
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
            SetTile(worldTilePosition, testTiles[1]);
        }
        else if (Input.GetMouseButton(1))
        {
            SetTile(worldTilePosition, testTiles[0]);   
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            SetTile(worldTilePosition, testTiles[2]);
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

                for (int i = 0; i < 4; i++)
                {
                    LightType lightType = (LightType) i;
                    int neighborLight = GetLight(neighborPosition, lightType);
                    
                    if (neighborLight <= 0)
                        continue;

                    switch (lightType)
                    {
                        case LightType.S:
                            sunLightPropagationQueue.Enqueue(neighborPosition);
                            break;
                        case LightType.R:
                            torchRedLightPropagationQueue.Enqueue(neighborPosition);
                            break;
                        case LightType.G:
                            torchGreenLightPropagationQueue.Enqueue(neighborPosition);
                            break;
                        case LightType.B:
                            torchBlueLightPropagationQueue.Enqueue(neighborPosition);
                            break;
                    }
                }
            }
        }
        else
        {
            int sunLight = GetLight(worldTilePosition, LightType.S);
            SetLight(worldTilePosition, 0, LightType.S);
            sunLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, sunLight));
            
            foreach (Vector2Int direction in TileUtil.Direction4)
            {
                Vector2Int neighborPosition = worldTilePosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                for (int i = 1; i < 4; i++)
                {
                    LightType lightType = (LightType) i;
                    int neighborLight = GetLight(neighborPosition, lightType);
                    
                    if (neighborLight <= 0)
                        continue;

                    switch (lightType)
                    {
                        case LightType.R:
                            torchRedLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, neighborLight));
                            break;
                        case LightType.G:
                            torchGreenLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, neighborLight));
                            break;
                        case LightType.B:
                            torchBlueLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, neighborLight));
                            break;
                    }
                }
            }
        }

        if (tile.emission.r > 0)
            torchRedLightPropagationQueue.Enqueue(worldTilePosition);

        if (tile.emission.g > 0)
            torchGreenLightPropagationQueue.Enqueue(worldTilePosition);

        if (tile.emission.b > 0)
            torchBlueLightPropagationQueue.Enqueue(worldTilePosition);

        if (beforeEmission.r > 0)
            torchRedLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, beforeEmission.r));

        if (beforeEmission.g > 0)
            torchGreenLightRemovalQueue.Enqueue(new Tuple<Vector2Int, int>(worldTilePosition, beforeEmission.g));

        if (beforeEmission.b > 0)
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

    public bool SetLight(Vector2Int worldTilePosition, int value, LightType type)
    {
        if (!TileUtil.BoundaryCheck(worldTilePosition, mapSize))
            return false;

        int index = TileUtil.To1DIndex(worldTilePosition, mapSize);

        if (lights[index].GetLight(type) == value)
            return false;
        
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            chunk.SetLightDirty();
        }
        else
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            newChunk.SetLightDirty();
        }

        lights[index].SetLight(value, type);

        return true;
    }

    public void SetEmission(Vector2Int worldTilePosition, LightEmission emission)
    {
        SetLight(worldTilePosition, emission.r, LightType.R);
        SetLight(worldTilePosition, emission.g, LightType.G);
        SetLight(worldTilePosition, emission.b, LightType.B);
    }

    public int GetLight(Vector2Int worldTilePosition, LightType type)
    {
        if (!TileUtil.BoundaryCheck(worldTilePosition, mapSize))
            return 0;
        
        return lights[TileUtil.To1DIndex(worldTilePosition, mapSize)].GetLight(type);
    }

    public bool SetTile(Vector2Int worldTilePosition, Tile tile)
    {
        if (!TileUtil.BoundaryCheck(worldTilePosition, mapSize))
            return false;

        int index = TileUtil.To1DIndex(worldTilePosition, mapSize);
        
        if (tiles[index].id == tile.id)
            return false;
        
        Vector2Int chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);

        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            chunk.SetMeshDirty();    
        }
        else
        {
            TileChunk newChunk = GenerateChunk(chunkPosition);
            newChunk.SetMeshDirty();
        }

        LightEmission beforeEmission = tiles[index].emission;
        tiles[index] = tile;

        SetEmission(worldTilePosition, tile.emission);
        CheckTileToUpdateLight(worldTilePosition, tile, beforeEmission);
        
        return true;
    }

    public bool GetTile(Vector2Int worldTilePosition, out Tile tile)
    {
        if (!TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            tile = Tile.Empty;
            return false;
        }

        tile = tiles[TileUtil.To1DIndex(worldTilePosition, mapSize)];
        return true;
    }
    
    TileChunk GenerateChunk(Vector2Int chunkPosition)
    {
        if (chunks.TryGetValue(chunkPosition, out TileChunk chunk))
        {
            return chunk;
        }

        TileChunk newChunk = new GameObject(chunkPosition.ToString()).AddComponent<TileChunk>();
        newChunk.Init(chunkPosition, this, chunkSize, mapSize, tileMaterial, lightMaterial);
        chunks.Add(chunkPosition, newChunk);
        
        return newChunk;
    }
}
