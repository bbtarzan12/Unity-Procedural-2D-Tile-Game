using System;
using System.Collections;
using System.Collections.Generic;
using OptIn.Tile;
using OptIn.Util;
using SimplexNoise;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using LightType = OptIn.Tile.LightType;
using Random = UnityEngine.Random;

public class TileManager : MonoBehaviour
{
    int[] tiles;
    TileLight[] lights;

    float[] waterDensities;

    [SerializeField] int2 mapSize;
    [SerializeField] int2 chunkSize;
    [SerializeField] int numUpdateChunkInFrame;
    [SerializeField] Material tileMaterial;
    [SerializeField] Material lightMaterial;
    
    [SerializeField] float maxFlow = 4.0f;
    [SerializeField] float minFlow = 0.005f;
    [SerializeField] float minDensity = 0.005f;
    [SerializeField] float maxDensity = 1.0f;
    [SerializeField] float maxCompress = 0.25f;
    [SerializeField] float flowSpeed = 0.5f;

    int2 numChunks;
    float currentFlowSpeed;
    Coroutine fluidUpdator;
    Queue<Tuple<int, float>> fluidQueue = new Queue<Tuple<int, float>>();

    Dictionary<int2, TileChunk> chunks = new Dictionary<int2, TileChunk>();
    Queue<int2> sunLightPropagationQueue = new Queue<int2>();
    Queue<Tuple<int2, int>> sunLightRemovalQueue = new Queue<Tuple<int2, int>>();

    Queue<int2> torchRedLightPropagationQueue = new Queue<int2>();
    Queue<int2> torchGreenLightPropagationQueue = new Queue<int2>();
    Queue<int2> torchBlueLightPropagationQueue = new Queue<int2>();

    Queue<Tuple<int2, int>> torchRedLightRemovalQueue = new Queue<Tuple<int2, int>>();
    Queue<Tuple<int2, int>> torchGreenLightRemovalQueue = new Queue<Tuple<int2, int>>();
    Queue<Tuple<int2, int>> torchBlueLightRemovalQueue = new Queue<Tuple<int2, int>>();

    NativeArray<int> nativeTiles;
    NativeArray<float> nativeWaterDensities;
    NativeArray<float> nativeWaterDiff;
    NativeArray<bool> nativeIsSolid;
    NativeList<int> nativeIndices;
    NativeArray<bool> nativeWaterChunkDirty;
    
    
    public int[] Tiles => tiles;
    public float[] WaterDensities => waterDensities;
    public TileLight[] Lights => lights;

    public static readonly Tile[] tileInformations =
    {
        new Tile{id = 0}, 
        new Tile{id = 1, color = new Color32(139, 192, 157, 255), attenuation = 50, isSolid = true},
        new Tile{id = 2, color = new Color32(72, 85, 255, 255), attenuation = 10},
        new Tile{id = 3, color = new Color32(255, 242, 161, 255), isSolid = true, emission = new LightEmission{r = TileLight.MaxTorchLight, g = TileLight.MaxTorchLight, b = 0}},
        new Tile{id = 4, color = new Color32(154, 255, 165, 255), isSolid = true, emission = new LightEmission{r = 0, g = TileLight.MaxTorchLight, b = 0}},
        new Tile{id = 5, color = new Color32(124, 153, 255, 255), isSolid = true, emission = new LightEmission{r = 0, g = 0, b = TileLight.MaxTorchLight}},
        new Tile{id = 6, color = new Color32(255, 141, 248, 255), isSolid = true, emission = new LightEmission{r = TileLight.MaxTorchLight, g = 0, b = TileLight.MaxTorchLight}},
    };

    const int waterID = 2;
    
    void Awake()
    {
        numChunks = mapSize / chunkSize;
        
        tiles = new int[mapSize.x * mapSize.y];
        lights = new TileLight[mapSize.x * mapSize.y];
        waterDensities = new float[mapSize.x * mapSize.y];
        
        nativeTiles = new NativeArray<int>(tiles.Length, Allocator.Persistent);
        nativeWaterDensities = new NativeArray<float>(waterDensities.Length, Allocator.Persistent);
        nativeWaterDiff = new NativeArray<float>(tiles.Length, Allocator.Persistent);
        nativeIsSolid = new NativeArray<bool>(tileInformations.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        nativeIndices = new NativeList<int>(Allocator.Persistent);
        nativeWaterChunkDirty = new NativeArray<bool>(numChunks.x * numChunks.y, Allocator.Persistent);
    }
    
    void Start()
    {
        GenerateTerrain();
        BuildSunLight();
    }

    void OnDestroy()
    {
        if(nativeTiles.IsCreated)
            nativeTiles.Dispose();
        if(nativeWaterDensities.IsCreated)
            nativeWaterDensities.Dispose();
        if(nativeWaterDiff.IsCreated)
            nativeWaterDiff.Dispose();
        if(nativeIsSolid.IsCreated)
            nativeIsSolid.Dispose();
        if(nativeIndices.IsCreated)
            nativeIndices.Dispose();
        if (nativeWaterChunkDirty.IsCreated)
            nativeWaterChunkDirty.Dispose();
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

                    float randomLight = Noise.CalcPixel2D(x, y, 0.03f);
                    if (randomLight >= 0.91f)
                    {
                        float randomType = Noise.CalcPixel2D(x, y, 0.01f);
                        SetTile(new int2(x, y), tileInformations[(int)TileUtil.MinMaxNormalization(randomType, 0, 1, 3, 6)].id);
                    }
                    else
                    {
                        SetTile(new int2(x, y), tileInformations[1].id);
                    }
                }
            }
        }
    }
    
    void BuildSunLight()
    {
        for (int x = 0; x < mapSize.x; x++)
        {
            int2 worldTilePosition = new int2(x, mapSize.y - 1);
            SetLight(worldTilePosition, TileLight.MaxSunLight, LightType.S);
            sunLightPropagationQueue.Enqueue(worldTilePosition);
        }
    }

    void Update()
    {
        currentFlowSpeed = Mathf.Clamp01(flowSpeed * Time.deltaTime);
        
        if(fluidUpdator == null)
            fluidUpdator = StartCoroutine(nameof(UpdateFluid));
        SunLightPropagation();
        TorchLightPropagation(ref torchRedLightPropagationQueue, ref torchRedLightRemovalQueue, LightType.R);
        TorchLightPropagation(ref torchGreenLightPropagationQueue, ref torchGreenLightRemovalQueue, LightType.G);
        TorchLightPropagation(ref torchBlueLightPropagationQueue, ref torchBlueLightRemovalQueue, LightType.B);
        UpdateChunks();

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        int2 worldTilePosition = TileUtil.WorldToWorldtile(mousePosition);

        if (!TileUtil.BoundaryCheck(worldTilePosition, mapSize))
            return;
        
        if (Input.GetMouseButton(0))
        {
            SetTile(worldTilePosition, tileInformations[1].id);
        }
        else if (Input.GetMouseButton(1))
        {
            SetTile(worldTilePosition, tileInformations[0].id);
            fluidQueue.Enqueue(new Tuple<int, float>(TileUtil.To1DIndex(worldTilePosition, mapSize), 0.0f));
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            SetTile(worldTilePosition, tileInformations[3].id);
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            SetTile(worldTilePosition, tileInformations[2].id);
            fluidQueue.Enqueue(new Tuple<int, float>(TileUtil.To1DIndex(worldTilePosition, mapSize), waterDensities[TileUtil.To1DIndex(worldTilePosition, mapSize)] + 3.0f));
        }
    }

    [BurstCompile]
    struct InitDiffJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> waterDiff;
        
        public void Execute(int index)
        {
            waterDiff[index] = 0.0f;
        }
    }
    
    [BurstCompile]
    struct InitDirtyJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<bool> waterChunkDirty;

        public void Execute(int index)
        {
            waterChunkDirty[index] = false;
        }
    }
    
    [BurstCompile]
    struct CalculateFluidJob : IJobParallelFor
    {
        [ReadOnly] public int2 mapSize;
        [ReadOnly] public int2 chunkSize;
        [ReadOnly] public int2 numChunks;
        [ReadOnly] public NativeArray<int> tiles;
        
        [ReadOnly] public float maxFlow;
        [ReadOnly] public float minFlow;
        [ReadOnly] public float minDensity;
        [ReadOnly] public float maxDensity;
        [ReadOnly] public float maxCompress;
        [ReadOnly] public float flowSpeed;
        
        [NativeDisableParallelForRestriction] public NativeArray<float> waterDensities;
        [NativeDisableParallelForRestriction] public NativeArray<float> waterDiff;
        [NativeDisableParallelForRestriction] public NativeArray<bool> isSolid;
        public NativeArray<bool> waterChunkDirty;


        public void Execute(int tileIndex)
        {
            int2 tilePosition = TileUtil.To2DIndex(tileIndex, mapSize);

            if (tiles[tileIndex] != waterID)
            {
                if (isSolid[tiles[tileIndex]])
                    waterDensities[tileIndex] = 0.0f;

                return;
            }

            if (waterDensities[tileIndex] < minDensity)
            {
                waterDensities[tileIndex] = 0;
                return;
            }

            float remainingDensity = waterDensities[tileIndex];

            if (remainingDensity <= 0)
                return;

            waterChunkDirty[TileUtil.To1DIndex(TileUtil.WorldTileToChunk(tilePosition, chunkSize), numChunks)] = true;

            int2 downPosition = tilePosition + TileUtil.Down;
            int downIndex = TileUtil.To1DIndex(downPosition, mapSize);
            if (TileUtil.BoundaryCheck(downPosition, mapSize) && !isSolid[tiles[downIndex]])
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
                return;
            }

            int2 leftPosition = tilePosition + TileUtil.Left;
            int leftIndex = TileUtil.To1DIndex(leftPosition, mapSize);
            if (TileUtil.BoundaryCheck(leftIndex, mapSize) && !isSolid[tiles[leftIndex]])
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
                return;
            }

            int2 rightPosition = tilePosition + TileUtil.Right;
            int rightIndex = TileUtil.To1DIndex(rightPosition, mapSize);
            if (TileUtil.BoundaryCheck(rightIndex, mapSize) && !isSolid[tiles[rightIndex]])
            {
                float flow = (remainingDensity - waterDensities[rightIndex]) / 3.0f;
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
                return;
            }

            int2 upPosition = tilePosition + TileUtil.Up;
            int upIndex = TileUtil.To1DIndex(upPosition, mapSize);
            if (TileUtil.BoundaryCheck(upIndex, mapSize) && !isSolid[tiles[upIndex]])
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
    }

    [BurstCompile]
    struct ApplyFluidJob : IJobParallelFor
    {
        [ReadOnly] public float minDensity;
        
        public NativeArray<float> waterDensities;
        public NativeArray<float> waterDiff;
        
        public void Execute(int index)
        {
            waterDensities[index] += waterDiff[index];

            if (waterDensities[index] < minDensity)
                waterDensities[index] = 0;
        }
    }

    [BurstCompile]
    struct FilterRemoveFluidJob : IJobParallelForFilter
    {
        [ReadOnly] public float minDensity;
        [ReadOnly] public NativeArray<float> waterDensities;
        [ReadOnly] public NativeArray<int> tiles;
        [ReadOnly] public NativeArray<bool> isSolid;

        public bool Execute(int index)
        {
            if (isSolid[tiles[index]])
                return false;

            return waterDensities[index] < minDensity && tiles[index] == waterID;
        }
    }

    [BurstCompile]
    struct FilterCreateFluidJob : IJobParallelForFilter
    {
        [ReadOnly] public float minDensity;
        [ReadOnly] public NativeArray<float> waterDensities;
        [ReadOnly] public NativeArray<int> tiles;
        [ReadOnly] public NativeArray<bool> isSolid;

        public bool Execute(int index)
        {
            if (isSolid[tiles[index]])
                return false;

            return waterDensities[index] >= minDensity && tiles[index] != waterID;
        }
    }

    IEnumerator UpdateFluid()
    {
        while (fluidQueue.Count != 0)
        {
            (int index, float density) = fluidQueue.Dequeue();
            
            if(TileUtil.BoundaryCheck(index, mapSize))
                waterDensities[index] = density;
        }
        
        nativeIndices.Clear();
        nativeTiles.CopyFrom(tiles);
        nativeWaterDensities.CopyFrom(waterDensities);

        for (int i = 0; i < nativeIsSolid.Length; i++)
        {
            nativeIsSolid[i] = tileInformations[i].isSolid;
        }
        
        InitDiffJob initJob = new InitDiffJob
        {
            waterDiff = nativeWaterDiff
        };
        
        initJob.Schedule(nativeWaterDiff.Length, 32).Complete();
        
        InitDirtyJob dirtyJob = new InitDirtyJob
        {
            waterChunkDirty = nativeWaterChunkDirty
        };
        
        dirtyJob.Schedule(nativeWaterChunkDirty.Length, 32).Complete();
        yield return null;
        
        CalculateFluidJob fluidJob = new CalculateFluidJob
        {
            mapSize = mapSize,
            chunkSize = chunkSize,
            numChunks = numChunks,
            maxFlow = maxFlow,
            minFlow = minFlow,
            minDensity = minDensity,
            maxDensity = maxDensity,
            maxCompress = maxCompress,
            flowSpeed = currentFlowSpeed,
            tiles = nativeTiles,
            waterDensities = nativeWaterDensities,
            waterDiff = nativeWaterDiff,
            isSolid = nativeIsSolid,
            waterChunkDirty = nativeWaterChunkDirty
        };

        fluidJob.Schedule(tiles.Length, 32).Complete();

        for (int i = 0; i < nativeWaterChunkDirty.Length; i++)
        {
            if(!nativeWaterChunkDirty[i])
                continue;

            if (chunks.TryGetValue(TileUtil.To2DIndex(i, numChunks), out TileChunk chunk))
            {
                chunk.SetMeshDirty();
            }
        }

        yield return null;
        
        ApplyFluidJob applyFluidJob = new ApplyFluidJob
        {
            minDensity = minDensity,
            waterDensities = nativeWaterDensities,
            waterDiff = nativeWaterDiff
        };
        
        applyFluidJob.Schedule(waterDensities.Length, 32).Complete();
        
        nativeWaterDensities.CopyTo(waterDensities);
        yield return null;

        FilterRemoveFluidJob filterRemoveFluidJob = new FilterRemoveFluidJob
        {
            tiles = nativeTiles,
            isSolid = nativeIsSolid,
            minDensity = minDensity,
            waterDensities = nativeWaterDensities
        };

        filterRemoveFluidJob.ScheduleAppend(nativeIndices, tiles.Length, 32).Complete();
        yield return null;

        foreach (int fluidIndex in nativeIndices)
        {
            waterDensities[fluidIndex] = 0.0f;
            SetTile(TileUtil.To2DIndex(fluidIndex, mapSize), tileInformations[0].id);
        }

        nativeIndices.Clear();
        FilterCreateFluidJob filterCreateFluidJob = new FilterCreateFluidJob
        {
            tiles = nativeTiles,
            isSolid = nativeIsSolid,
            minDensity = minDensity,
            waterDensities = nativeWaterDensities
        };
        
        filterCreateFluidJob.ScheduleAppend(nativeIndices, tiles.Length, 32).Complete();
        yield return null;
        
        foreach (int fluidIndex in nativeIndices)
        {
            SetTile(TileUtil.To2DIndex(fluidIndex, mapSize), waterID);
        }
        
        fluidUpdator = null;
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

    void CheckTileToUpdateLight(int2 worldTilePosition, int id, LightEmission beforeEmission)
    {
        if (id == 0)
        {
            foreach (int2 direction in TileUtil.Direction4)
            {
                int2 neighborPosition = worldTilePosition + direction;

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
            sunLightRemovalQueue.Enqueue(new Tuple<int2, int>(worldTilePosition, sunLight));
            
            foreach (int2 direction in TileUtil.Direction4)
            {
                int2 neighborPosition = worldTilePosition + direction;

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
                            torchRedLightRemovalQueue.Enqueue(new Tuple<int2, int>(worldTilePosition, neighborLight));
                            break;
                        case LightType.G:
                            torchGreenLightRemovalQueue.Enqueue(new Tuple<int2, int>(worldTilePosition, neighborLight));
                            break;
                        case LightType.B:
                            torchBlueLightRemovalQueue.Enqueue(new Tuple<int2, int>(worldTilePosition, neighborLight));
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
            torchRedLightRemovalQueue.Enqueue(new Tuple<int2, int>(worldTilePosition, beforeEmission.r));

        if (beforeEmission.g > 0)
            torchGreenLightRemovalQueue.Enqueue(new Tuple<int2, int>(worldTilePosition, beforeEmission.g));

        if (beforeEmission.b > 0)
            torchBlueLightRemovalQueue.Enqueue(new Tuple<int2, int>(worldTilePosition, beforeEmission.b));
    }

    public void SunLightPropagation()
    {
        while (sunLightRemovalQueue.Count != 0)
        {
            (int2 lightPosition, int sunLight) = sunLightRemovalQueue.Dequeue();

            foreach (int2 direction in TileUtil.Direction4)
            {
                int2 neighborPosition = lightPosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborSunLight = GetLight(neighborPosition, LightType.S);

                if (neighborSunLight != 0 && neighborSunLight < sunLight || direction.Equals(TileUtil.Down) && sunLight == TileLight.MaxSunLight)
                {
                    SetLight(neighborPosition, 0, LightType.S);
                    sunLightRemovalQueue.Enqueue(new Tuple<int2, int>(neighborPosition, neighborSunLight));
                }
                else if (neighborSunLight >= sunLight)
                {
                    sunLightPropagationQueue.Enqueue(neighborPosition);
                }
            }
        }

        while (sunLightPropagationQueue.Count != 0)
        {
            int2 lightPosition = sunLightPropagationQueue.Dequeue();
            int sunLight = GetLight(lightPosition, LightType.S);

            if (sunLight <= 0)
                continue;

            foreach (int2 direction in TileUtil.Direction4)
            {
                int2 neighborPosition = lightPosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborSunLight = GetLight(neighborPosition, LightType.S);

                int resultSunLight = sunLight - TileLight.SunLightAttenuation;

                int neighborTile = GetTile(neighborPosition);
                
                bool isOpacity = neighborTile != -1 && neighborTile != 0;

                if (isOpacity)
                    resultSunLight -= tileInformations[neighborTile].attenuation;

                if (direction.Equals(TileUtil.Down) && !isOpacity && sunLight == TileLight.MaxSunLight)
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

    public void TorchLightPropagation(ref Queue<int2> propagationQueue, ref Queue<Tuple<int2, int>> removalQueue, LightType lightType)
    {
        while (removalQueue.Count != 0)
        {
            (int2 lightPosition, int torchLight) = removalQueue.Dequeue();

            foreach (int2 direction in TileUtil.Direction4)
            {
                int2 neighborPosition = lightPosition + direction;

                if (!TileUtil.BoundaryCheck(neighborPosition, mapSize))
                    continue;

                int neighborTorchLight = GetLight(neighborPosition, lightType);

                if (neighborTorchLight != 0 && neighborTorchLight < torchLight)
                {
                    SetLight(neighborPosition, 0, lightType);
                    removalQueue.Enqueue(new Tuple<int2, int>(neighborPosition, neighborTorchLight));
                }
                else if (neighborTorchLight >= torchLight)
                {
                    propagationQueue.Enqueue(neighborPosition);
                }
            }
        }

        while (propagationQueue.Count != 0)
        {
            int2 lightPosition = propagationQueue.Dequeue();
            int torchLight = GetLight(lightPosition, lightType);

            if (torchLight <= 0)
                continue;

            foreach (int2 direction in TileUtil.Direction4)
            {
                int2 neighborPosition = lightPosition + direction;

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

    public bool SetLight(int2 worldTilePosition, int value, LightType type)
    {
        if (!TileUtil.BoundaryCheck(worldTilePosition, mapSize))
            return false;

        int index = TileUtil.To1DIndex(worldTilePosition, mapSize);

        if (lights[index].GetLight(type) == value)
            return false;
        
        int2 chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);
        
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

    public void SetEmission(int2 worldTilePosition, LightEmission emission)
    {
        SetLight(worldTilePosition, emission.r, LightType.R);
        SetLight(worldTilePosition, emission.g, LightType.G);
        SetLight(worldTilePosition, emission.b, LightType.B);
    }

    public int GetLight(int2 worldTilePosition, LightType type)
    {
        if (!TileUtil.BoundaryCheck(worldTilePosition, mapSize))
            return 0;
        
        return lights[TileUtil.To1DIndex(worldTilePosition, mapSize)].GetLight(type);
    }

    public bool SetTile(int2 worldTilePosition, int id)
    {
        if (!TileUtil.BoundaryCheck(worldTilePosition, mapSize))
            return false;

        int index = TileUtil.To1DIndex(worldTilePosition, mapSize);
        
        if (tiles[index] == id)
            return false;
        
        int2 chunkPosition = TileUtil.WorldTileToChunk(worldTilePosition, chunkSize);

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
            fluidQueue.Enqueue(new Tuple<int, float>(index, 0.0f));

        LightEmission beforeEmission = tileInformations[tiles[index]].emission;
        tiles[index] = id;

        SetEmission(worldTilePosition, tileInformations[id].emission);
        CheckTileToUpdateLight(worldTilePosition, id, beforeEmission);
        
        return true;
    }

    public int GetTile(int2 worldTilePosition)
    {
        if (!TileUtil.BoundaryCheck(worldTilePosition, mapSize))
        {
            return -1;
        }
        
        return tiles[TileUtil.To1DIndex(worldTilePosition, mapSize)];
    }
    
    TileChunk GenerateChunk(int2 chunkPosition)
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

    void OnDrawGizmos()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        int2 worldtilePosition = TileUtil.WorldToWorldtile(mousePosition);
        if (!TileUtil.BoundaryCheck(worldtilePosition, mapSize))
            return;

        int tileIndex = TileUtil.To1DIndex(worldtilePosition, mapSize);
        int tile = tiles[tileIndex];

        if (tileInformations[tile].isSolid)
            return;
        
        Handles.Label(mousePosition, waterDensities[tileIndex].ToString());
    }
}
