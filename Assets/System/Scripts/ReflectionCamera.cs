using UnityEngine;

public class ReflectionCamera : MonoBehaviour
{
	public Camera reflectionCamera;
	//public Material material;

	void OnWillRenderObject()
	{
		if (reflectionCamera != null)// && material != null)
		{
			// Set up reflection camera
			SetReflectionCamera();

			// Render the scene through the reflection camera
			reflectionCamera.Render();

			// Apply the reflection texture to the material
			//material.SetTexture("_DynReflTex", reflectionCamera.targetTexture);
		}
	}

	private void SetReflectionCamera()
	{
		if (reflectionCamera != null)
		{
			// Calculate the reflection plane (Y-axis is inverted)
			float distance = Vector3.Dot(transform.position, transform.up) * 2; //Y axis is inverted
			reflectionCamera.transform.position = new Vector3(transform.position.x, distance + transform.position.y, transform.position.z);

			// Set the view matrix
			Vector3 cameraPosition = reflectionCamera.transform.position;
			Quaternion rotation = Quaternion.Euler(180, 0, 0); // Rotate 180 degrees around X
			//Matrix4x4 viewMatrix = Matrix4x4.LookAtMatrix(cameraPosition, transform.position, Vector3.Cross(transform.right, cameraPosition - transform.position).normalized);

			//reflectionCamera.worldToCameraMatrix = viewMatrix; // Set the reflection camera's view matrix
		}
	}
}