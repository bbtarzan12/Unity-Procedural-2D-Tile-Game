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

        Vector2Int chunkSize;

        void Awake()
        {
            mesh = new Mesh();
            filter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            gameObject.layer = LayerMask.NameToLayer("Light");
        }

        public void Init(Vector2Int size, Material lightMaterial)
        {
            meshRenderer.sharedMaterial = lightMaterial;
            chunkSize = size;

            texture = new Texture2D(chunkSize.x, chunkSize.y, TextureFormat.RGBA32, false);
            
            Vector3[] quadVertices =
            {
                new Vector2(0, 0) * chunkSize,
                new Vector2(0, 1) * chunkSize, 
                new Vector2(1, 0) * chunkSize, 
                new Vector2(1, 1) * chunkSize
            };

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
                    Vector2Int lightPosition = new Vector2Int(x, y);

                    int index = TileUtil.To1DIndex(lightPosition, chunkSize);

                    int sunLight = lights[index].GetSunLight();
                    int torchLight = lights[index].GetTorchLight();

                    byte lightIntensity = (byte)((TileLight.MaxSunLight - Mathf.Max(sunLight, torchLight)) * 17);

                    Color32 color = new Color32(0, 0, 0, lightIntensity); 
                    texture.SetPixel(x, y, color);
                }
            }

            texture.alphaIsTransparency = true;
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            
            meshRenderer.material.mainTexture = texture;
        }
    }
}