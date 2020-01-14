using System.Collections.Generic;
using OptIn.Util;
using UnityEngine;

namespace OptIn.Tile
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(PolygonCollider2D))]
    [RequireComponent(typeof(MeshRenderer))]
    public class TileMesh : MonoBehaviour
    {

        Mesh mesh;
        MeshFilter filter;
        PolygonCollider2D polygonCollider;
        MeshRenderer meshRenderer;

        Vector2Int chunkPosition;
        Vector2Int chunkSize;
        Vector2Int mapSize;

        List<List<Vector2>> paths = new List<List<Vector2>>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        List<Vector4> uvs = new List<Vector4>();
        List<Color32> colors = new List<Color32>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        
        void Awake()
        {
            mesh = new Mesh();
            filter = GetComponent<MeshFilter>();
            polygonCollider = GetComponent<PolygonCollider2D>();
            meshRenderer = GetComponent<MeshRenderer>();
            gameObject.layer = LayerMask.NameToLayer("Terrain");
        }

        public void Init(Vector2Int chunkSize, Vector2Int mapSize, Vector2Int chunkPosition, Material tileMaterial)
        {
            meshRenderer.sharedMaterial = tileMaterial;
            this.chunkSize = chunkSize;
            this.mapSize = mapSize;
            this.chunkPosition = chunkPosition;
        }

        public void UpdateMesh(int[] tiles, float[] waterDensities)
        {
            GenerateMesh(tiles, waterDensities);
            
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.SetUVs(0, uvs);
            
            filter.mesh = mesh;

            polygonCollider.pathCount = paths.Count;
            for(int i = 0 ; i < paths.Count; i++)
                polygonCollider.SetPath(i, paths[i]);
        }

        void GenerateMesh(int[] tiles, float[] waterDensities)
        {
            vertices.Clear();
            indices.Clear();
            uvs.Clear();
            colors.Clear();
            paths.Clear();;
            visited.Clear();

            int numQuads = 0;

            for (int x = 0; x < chunkSize.x; x++)
            {
                for (int y = 0; y < chunkSize.y;)
                {
                    Vector2Int tilePosition = TileUtil.TileToWorldTile(new Vector2Int(x, y), chunkPosition, chunkSize);
                    int index = TileUtil.To1DIndex(tilePosition, mapSize);
                    int tile = tiles[index];

                    if (tile == 0)
                    {
                        y++;
                        continue;
                    }

                    if (visited.Contains(tilePosition))
                    {
                        y++;
                        continue;
                    }

                    visited.Add(tilePosition);

                    int height;
                    for (height = 1; height + y < chunkSize.y; height++)
                    {
                        Vector2Int nextPosition = tilePosition + Vector2Int.up * height;
                        
                        if(!TileUtil.BoundaryCheck(nextPosition, chunkPosition, chunkSize))
                            break;
                        
                        int nextIndex = TileUtil.To1DIndex(nextPosition, mapSize);

                        int nextTile = tiles[nextIndex];

                        if (nextTile != tile)
                            break;
                        
                        if (visited.Contains(nextPosition))
                            break;

                        visited.Add(nextPosition);
                    }

                    bool done = false;
                    int width;
                    for (width = 1; width + x < chunkSize.x; width++)
                    {
                        for (int dy = 0; dy < height; dy++)
                        {
                            Vector2Int nextPosition = tilePosition + Vector2Int.up * dy + Vector2Int.right * width;

                            if (!TileUtil.BoundaryCheck(nextPosition, chunkPosition, chunkSize))
                            {
                                done = true;
                                break;
                            }
                        
                            int nextIndex = TileUtil.To1DIndex(nextPosition, mapSize);
                            
                            int nextTile = tiles[nextIndex];

                            if (nextTile != tile || visited.Contains(nextPosition))
                            {
                                done = true;
                                break;
                            }
                        }

                        if (done)
                        {
                            break;
                        }

                        for (int dy = 0; dy < height; dy++)
                        {
                            Vector2Int nextPosition = 
                                tilePosition + 
                                Vector2Int.up * dy + 
                                Vector2Int.right * width;
                            visited.Add(nextPosition);
                        }
                    }

                    Vector2 scale = new Vector2(width, height);

                    List<Vector2> points = new List<Vector2>();
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 vertex = tileVertices[i] * scale + tilePosition;
                        vertices.Add(vertex);
                        uvs.Add(tileVertices[i] * scale);

                        Color32 color = TileManager.tileInformations[tile].color;
                        if(TileManager.tileInformations[tile].isSolid)
                            colors.Add(color);
                        else
                        {
                            float density = Mathf.Clamp(waterDensities[index], 0.3f, 1.0f);
                            color.a = (byte) (byte.MaxValue * density);
                            colors.Add(color);
                        }   
                        
                        
                        // Not Optimized Collider Generation
                        Vector2 point = colliderPoints[i] * scale + tilePosition;
                        points.Add(point);
                    }
                    paths.Add(points);

                    for (int i = 0; i < 6; i++)
                    {
                        indices.Add(tileIndices[i] + numQuads * 4);
                    }

                    y += height;
                    numQuads++;
                }
            }
        }


        public static readonly int[] tileIndices =
        {
            0, 1, 3,
            0, 3, 2
        };
        
        public static readonly Vector2[] tileVertices =
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 0),
            new Vector2(1, 1) 
        };

        public static readonly Vector2[] colliderPoints =
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 1), 
            new Vector2(1, 0)
        };
    }   
}