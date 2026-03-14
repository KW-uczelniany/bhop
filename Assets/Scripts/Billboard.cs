using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Transform camTransform;

    void Start()
    {
        if (camTransform == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                camTransform = cam.transform;
        }
    }

    void LateUpdate()
    {
        if (camTransform != null)
        {
            // Make the sprite face the camera exactly
            transform.LookAt(transform.position + camTransform.forward);
        }
    }
}
