using System;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{

    Camera camera;

    [SerializeField] float scrollSpeed = 2.5f;
    [SerializeField] float dragSpeed = 1.0f;

    void Awake()
    {
        camera = GetComponent<Camera>();
    }
    
    void Update()
    {
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");

        if (scroll < 0)
        {
            camera.orthographicSize += 1.0f * scrollSpeed;
        }
        else if (scroll > 0)
        {
            camera.orthographicSize -= 1.0f * scrollSpeed;
        }

        if (camera.orthographicSize < 4)
            camera.orthographicSize = 4;
    }

    void LateUpdate()
    {
        if(Input.GetMouseButton(2))
            transform.position -= new Vector3(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"), 0) * (dragSpeed * camera.orthographicSize * 0.01f);
    }
}