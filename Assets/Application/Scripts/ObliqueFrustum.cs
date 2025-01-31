//ObliqueFrustum.cs
using UnityEngine;

[ExecuteInEditMode]
public class ObliqueFrustum : MonoBehaviour
{
    [Range(-1, 1)] public float offsetX = 0;
    [Range(-1, 1)] public float offsetY = 0;

    Camera[] cameras => FindObjectsOfType<Camera>();

    void Update()
    {
		foreach (var cam in cameras) ApplyToCamera(cam);
	}

	void ApplyToCamera(Camera camera)
    {
        camera.ResetProjectionMatrix();
        Matrix4x4 matrix = camera.projectionMatrix;
        matrix[0, 2] = offsetX;
        matrix[1, 2] = offsetY;
        camera.projectionMatrix = matrix;
    }
}
