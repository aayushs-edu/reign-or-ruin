using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MousePos : MonoBehaviour
{

    private void Start()
    {
        // Ensure the camera is set to orthographic for 2D
        Camera.main.orthographic = true;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 0; // Set z to 0 for 2D
        transform.position = mousePos;
    }
}
