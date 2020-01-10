using OptIn.Util;
using UnityEngine;

namespace OptIn.Tile
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class TileLightTexture : MonoBehaviour
    {

        Mesh mesh;
        Texture2D texture;
        MeshFilter filter;
        MeshRenderer meshRenderer;

        Vector2Int chunkPosition;
        Vector2Int chunkSize;
        Vector2Int mapSize;

        void Awake()
        {
            mesh = new Mesh();
            filter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            gameObject.layer = LayerMask.NameToLayer("Light");
        }

        public void Init(Vector2Int chunkSize, Vector2Int mapSize, Vector2Int chunkPosition, Material lightMaterial)
        {
            meshRenderer.sharedMaterial = lightMaterial;
            this.chunkSize = chunkSize;
            this.mapSize = mapSize;
            this.chunkPosition = chunkPosition;
            
            texture = new Texture2D(chunkSize.x, chunkSize.y, TextureFormat.RGBA32, false);
            texture.alphaIsTransparency = true;
            texture.filterMode = FilterMode.Point;
            meshRenderer.material.mainTexture = texture;

            Vector3[] quadVertices = {new Vector2(0, 0) * chunkSize, new Vector2(0, 1) * chunkSize, new Vector2(1, 0) * chunkSize, new Vector2(1, 1) * chunkSize};

            mesh.SetVertices(quadVertices);
            mesh.SetIndices(TileMesh.tileIndices, MeshTopology.Triangles, 0);
            mesh.SetUVs(0, TileMesh.tileVertices);

            filter.mesh = mesh;
        }

        public void UpdateTexture(TileLight[] lights)
        {
            for (int x = 0; x < chunkSize.x; x++)
            {
                for (int y = 0; y < chunkSize.y; y++)
                {
                    Vector2Int lightPosition = TileUtil.TileToWorldTile(new Vector2Int(x, y), chunkPosition, chunkSize);

                    int index = TileUtil.To1DIndex(lightPosition, mapSize);

                    int sunLight = lights[index].GetSunLight();

                    int torchRedLight = lights[index].GetRedLight();
                    int torchGreenLight = lights[index].GetGreenLight();
                    int torchBlueLight = lights[index].GetBlueLight();
                    
                    byte redIntensity = (byte) Mathf.Max(torchRedLight, sunLight);
                    byte greenIntensity = (byte) Mathf.Max(torchGreenLight, sunLight);
                    byte blueIntensity = (byte) Mathf.Max(torchBlueLight, sunLight);

                    Color32 color = new Color32(redIntensity, greenIntensity, blueIntensity, 255);
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
        }
    }
}