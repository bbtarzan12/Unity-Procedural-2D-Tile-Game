using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SmoothLighting : MonoBehaviour
{

    [SerializeField] Camera mainCamera;
    [SerializeField] Transform quad;
    [SerializeField] Material blurMaterial;

    Camera lightCamera;
    
    void Awake()
    {
        lightCamera = GetComponent<Camera>();
    }
    
    void LateUpdate()
    {
        Vector3 position = mainCamera.transform.position;
        
        transform.position = position;
        lightCamera.orthographicSize = mainCamera.orthographicSize;
        
        float orthographicSize = lightCamera.orthographicSize;

        blurMaterial.SetFloat("_BlurSize", Mathf.Lerp(0.15f, 0.05f, (orthographicSize - 25f) / 75f));

        position.z += 10;
        quad.transform.position = position;
        
        quad.localScale = new Vector3
        {
            x = orthographicSize * 2.0f * Screen.width / Screen.height, 
            y = orthographicSize * 2.0f,
            z = 0.1f
        };
    }
    
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        RenderTexture temporaryTexture = RenderTexture.GetTemporary(src.width, src.height);
        Graphics.Blit(src, temporaryTexture, blurMaterial, 0);
        Graphics.Blit(temporaryTexture, dest, blurMaterial, 1);
        RenderTexture.ReleaseTemporary(temporaryTexture);
    }
}
