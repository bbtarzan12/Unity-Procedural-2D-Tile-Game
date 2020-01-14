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
    int[] tiles;
    TileLight[] lights;

    float[] waterDensities;
    float[] waterDiff;

    [SerializeField] Vector2Int mapSize;
    [SerializeField] Vector2Int chunkSize;
    [SerializeField] int numUpdateChunkInFrame;
    [SerializeField] Material tileMaterial;
    [SerializeField] Material lightMaterial;
    
    public float maxFlow = 4.0f;
    public float minFlow = 0.005f;
    public float minDensity = 0.005f;
    public float maxDensity = 1.0f;
    public float maxCompress = 0.25f;
    public float flowSpeed = 0.5f;

    Dictionary<Vector2Int, TileChunk> chunks = new Dictionary<Vector2Int, TileChunk>();
    Queue<Vector2Int> sunLightPropagationQueue = new Queue<Vector2Int>();
    Queue<Tuple<Vector2Int, int>> sunLightRemovalQueue = new Queue<Tuple<Vector2Int, int>>();

    Queue<Vector2Int> torchRedLightPropagationQueue = new Queue<Vector2Int>();
    Queue<Vector2Int> torchGreenLightPropagationQueue = new Queue<Vector2Int>();
    Queue<Vector2Int> torchBlueLightPropagationQueue = new Queue<Vector2Int>();

    Queue<Tuple<Vector2Int, int>> torchRedLightRemovalQueue = new Queue<Tuple<Vector2Int, int>>();
    Queue<Tuple<Vector2Int, int>> torchGreenLightRemovalQueue = new Queue<Tuple<Vector2Int, int>>();
    Queue<Tuple<Vector2Int, int>> torchBlueLightRemovalQueue = new Queue<Tuple<Vector2Int, int>>();
    
    public int[] Tiles => tiles;
    public float[] WaterDensities => waterDensities;
    public TileLight[] Lights => lights;

    public static readonly Tile[] tileInformations =
    {
        new Tile{id = 0}, 
        new Tile{id = 1, color = new Color32(139, 192, 157, 255), attenuation = 50, isSolid = true},
        new Tile{id = 2, color = new Color32(255, 242, 161, 255), isSolid = true, emission = new LightEmission{r = TileLight.MaxTorchLight, g = TileLight.MaxTorchLight, b = 0}},
        new Tile{id = 3, color = new Color32(72, 85, 255, 255), attenuation = 10}
    };

    const int waterID = 3;
    
    void Awake()
    {
        tiles = new int[mapSize.x * mapSize.y];
        lights = new TileLight[mapSize.x * mapSize.y];
        waterDensities = new float[mapSize.x * mapSize.y];
        waterDiff = new float[mapSize.x * mapSize.y];
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
                    
                    SetTile(new Vector2Int(x, y), tileInformations[1].id);
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
        UpdateFluidTiles();
        SunLightPropagation();
        TorchLightPropagation(ref torchRedLightPropagationQueue, ref torchRedLightRemovalQueue, LightType.R);
        TorchLightPropagation(ref torchGreenLightPropagationQueue, ref torchGreenLightRemovalQueue, LightType.G);
        TorchLightPropagation(ref torchBlueLightPropagationQueue, ref torchBlueLightRemovalQueue, LightType.B);
        UpdateChunks();

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int worldTilePosition = TileUtil.WorldToWorldtile(mousePosition);
        if (Input.GetMouseButton(0))
        {
            SetTile(worldTilePosition, tileInformations[1].id);
        }
        else if (Input.GetMouseButton(1))
        {
            SetTile(worldTilePosition, tileInformations[0].id);
            waterDensities[TileUtil.To1DIndex(worldTilePosition, mapSize)] = 0.0f;
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            SetTile(worldTilePosition, tileInformations[2].id);
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            SetTile(worldTilePosition, tileInformations[3].id);
            waterDensities[TileUtil.To1DIndex(worldTilePosition, mapSize)] += 1.0f;
        }
    }

    void UpdateFluidTiles()
    {
        Array.Clear(waterDiff, 0, waterDiff.Length);

        for (int y = 0, tileIndex = 0; y < mapSize.y; y++)
        {
            for (int x = 0; x < mapSize.x; x++, tileIndex++)
            {
                Vector2Int tilePosition = new Vector2Int(x, y);

                if (tiles[tileIndex] != waterID)
                {
                    if (tileInformations[tiles[tileIndex]].isSolid)
                        waterDensities[tileIndex] = 0.0f;

                    continue;
                }

                if (waterDensities[tileIndex] < minDensity)
                {
                    waterDensities[tileIndex] = 0;
                    continue;
                }

                float remainingDensity = waterDensities[tileIndex];

                if (remainingDensity <= 0)
                    continue;

                Vector2Int downPosition = tilePosition + Vector2Int.down;
                int downIndex = TileUtil.To1DIndex(downPosition, mapSize);
                if (TileUtil.BoundaryCheck(downPosition, mapSize) && !tileInformations[tiles[downIndex]].isSolid)
                {
                    float flow = CalculateStableDensity(remainingDensity + waterDensities[downIndex]) - waterDensities[downIndex];
                    if (flow > minFlow)
                        flow *= flowSpeed;

                    flow = Mathf.Clamp(flow, 0, Mathf.Min(maxFlow, remainingDensity));
                    waterDiff[tileIndex] -= flow;
                    waterDiff[downIndex] += flow;
                    remainingDensity -= flow;
                }

                if (remainingDensity < minDensity)
                {
                    waterDiff[tileIndex] -= remainingDensity;
                    continue;
                }

                Vector2Int leftPosition = tilePosition + Vector2Int.left;
                int leftIndex = TileUtil.To1DIndex(leftPosition, mapSize);
                if (TileUtil.BoundaryCheck(leftIndex, mapSize) && !tileInformations[tiles[leftIndex]].isSolid)
                {
                    float flow = (remainingDensity - waterDensities[leftIndex]) / 4.0f;
                    if (flow > minFlow)
                        flow *= flowSpeed;

                    flow = Mathf.Clamp(flow, 0, Mathf.Min(maxFlow, remainingDensity));
                    waterDiff[tileIndex] -= flow;
                    waterDiff[leftIndex] += flow;
                    remainingDensity -= flow;
                }

                if (remainingDensity < minDensity)
                {
                    waterDiff[tileIndex] -= remainingDensity;
                    continue;
                }

                Vector2Int rightPosition = tilePosition + Vector2Int.right;
                int rightIndex = TileUtil.To1DIndex(rightPosition, mapSize);
                if (TileUtil.BoundaryCheck(rightIndex, mapSize) && !tileInformations[tiles[rightIndex]].isSolid)
                {
                    float flow = (remainingDensity - waterDensities[rightIndex]) / 4.0f;
                    if (flow > minFlow)
                        flow *= flowSpeed;

                    flow = Mathf.Clamp(flow, 0, Mathf.Min(maxFlow, remainingDensity));
                    waterDiff[tileIndex] -= flow;
                    waterDiff[rightIndex] += flow;
                    remainingDensity -= flow;
                }

                if (remainingDensity < minDensity)
                {
                    waterDiff[tileIndex] -= remainingDensity;
                    continue;
                }

                Vector2Int upPosition = tilePosition + Vector2Int.up;
                int upIndex = TileUtil.To1DIndex(upPosition, mapSize);
                if (TileUtil.BoundaryCheck(upIndex, mapSize) && !tileInformations[tiles[upIndex]].isSolid)
                {
                    float flow = remainingDensity - CalculateStableDensity(remainingDensity + waterDensities[upIndex]);
                    if (flow > minFlow)
                        flow *= flowSpeed;

                    flow = Mathf.Clamp(flow, 0, Mathf.Min(maxFlow, remainingDensity));
                    waterDiff[tileIndex] -= flow;
                    waterDiff[upIndex] += flow;
                    remainingDensity -= flow;
                }

                if (remainingDensity < minDensity)
                {
                    waterDiff[tileIndex] -= remainingDensity;
                    continue;
                }
            }
        }

        for (int i = 0; i < tiles.Length; i++)
        {
            if(tileInformations[tiles[i]].isSolid)
                continue;
            
            waterDensities[i] += waterDiff[i];

            if (waterDensities[i] < minDensity && tiles[i] == waterID)
            {
                waterDensities[i] = 0.0f;
                SetTile(TileUtil.To2DIndex(i, mapSize), tileInformations[0].id);
            }
            else if(waterDensities[i] >= minDensity && tiles[i] != waterID)
            {
                SetTile(TileUtil.To2DIndex(i, mapSize), waterID);
            }
        }
    }

    float CalculateStableDensity(float totalDensity)
    {
        if (totalDensity <= maxDensity)
            return maxDensity;
        else if (totalDensity < 2 * maxDensity + maxCompress)
            return (maxDensity * maxDensity + totalDensity * maxCompress) / (maxDensity + maxCompress);
        else
            return (totalDensity + maxCompress) / 2f;
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

    void CheckTileToUpdateLight(Vector2Int worldTilePosition, int id, LightEmission beforeEmission)
    {
        if (id == 0)
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

        if (tileInformations[id].emission.r > 0)
            torchRedLightPropagationQueue.Enqueue(worldTilePosition);

        if (tileInformations[id].emission.g > 0)
            torchGreenLightPropagationQueue.Enqueue(worldTilePosition);

        if (tileInformations[id].emission.b > 0)
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

                int neighborTile = GetTile(neighborPosition);
                
                bool isOpacity = neighborTile != -1 && neighborTile != 0;

                if (isOpacity)
                    resultSunLight -= tileInformations[neighborTile].attenuation;

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

                int neighborTile = GetTile(neighborPosition);
                
                bool isOpacity = neighborTile != -1 && neighborTile != 0;

                if (isOpacity)
                    resultTorchLight -= tileInformations[neighborTile].attenuation;

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

    public bool SetTile(Vector2Int worldTilePosition, int id)
    {
        if (!TileUtil.BoundaryCheck(worldTilePosition, mapSize))
            return false;

        int index = TileUtil.To1DIndex(worldTilePosition, mapSize);
        
        if (tiles[index] == id)
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
        
        if (tileInformations[id].isSolid)
            waterDensities[index] = 0.0f;

        LightEmission beforeEmission = tileInformations[tiles[index]].emission;
        tiles[index] = id;

        SetEmission(worldTilePosition, tileInformations[id].emission);
        CheckTileToUpdateLight(worldTilePosition, id, beforeEmission);
        
        return true;
    }

    public int GetTile(Vector2Int worldTilePosition)
    {
        if (!TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            return -1;
        }
        
        return tiles[TileUtil.To1DIndex(worldTilePosition, mapSize)];
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
