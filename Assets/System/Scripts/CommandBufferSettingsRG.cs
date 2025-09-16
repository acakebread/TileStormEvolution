using UnityEngine;
using UnityEngine.Rendering;

public class CommandBufferSettingsRG : MonoBehaviour
{
	public delegate void RenderCommand(RasterCommandBuffer commandBuffer);
	public event RenderCommand OnBeforeRender;
	public event RenderCommand OnAfterTransparent;
	public event RenderCommand OnAfterRender;

	public void ExecuteBeforeRender(RasterCommandBuffer commandBuffer, Camera camera)
	{
		if (GetComponent<Camera>() == camera)
		{
			//Debug.Log($"Executing OnBeforeRender for camera: {camera.name}");
			OnBeforeRender?.Invoke(commandBuffer);
		}
		else
		{
			Debug.Log($"Skipping OnBeforeRender for camera: {camera.name} (Expected: {GetComponent<Camera>()?.name})");
		}
	}

	public void ExecuteAfterTransparentRender(RasterCommandBuffer commandBuffer, Camera camera)
	{
		if (GetComponent<Camera>() == camera)
		{
			//Debug.Log($"Executing OnAfterRender for camera: {camera.name}");
			OnAfterTransparent?.Invoke(commandBuffer);
		}
		else
		{
			Debug.Log($"Skipping OnAfterTransparent for camera: {camera.name} (Expected: {GetComponent<Camera>()?.name})");
		}
	}

	public void ExecuteAfterRender(RasterCommandBuffer commandBuffer, Camera camera)
	{
		if (GetComponent<Camera>() == camera)
		{
			//Debug.Log($"Executing OnAfterRender for camera: {camera.name}");
			OnAfterRender?.Invoke(commandBuffer);
		}
		else
		{
			Debug.Log($"Skipping OnAfterRender for camera: {camera.name} (Expected: {GetComponent<Camera>()?.name})");
		}
	}
}