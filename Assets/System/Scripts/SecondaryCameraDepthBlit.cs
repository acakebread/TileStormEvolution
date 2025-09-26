using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class SecondaryCameraDepthBlit : MonoBehaviour
{
	public Camera mainCamera;
	public Camera secondaryCamera;

	private RenderTexture colorRT;
	private RenderTexture depthRT;

	void Start()
	{
		if (mainCamera == null) mainCamera = Camera.main;

		// Create a RenderTexture with depth
		colorRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.DefaultHDR);
		depthRT = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth);

		secondaryCamera.targetTexture = colorRT;
		secondaryCamera.SetTargetBuffers(colorRT.colorBuffer, depthRT.depthBuffer);

		secondaryCamera.clearFlags = CameraClearFlags.SolidColor;
		secondaryCamera.backgroundColor = Color.clear;
	}

	void LateUpdate()
	{
		// Render the secondary camera into its RT
		secondaryCamera.Render();

		// Copy depth from secondary camera to main camera
		Graphics.Blit(depthRT, (RenderTexture)null, new Material(Shader.Find("Hidden/CopyDepth")));

		// Draw secondary camera color over main camera
		Graphics.Blit(colorRT, (RenderTexture)null);
	}

	void OnDestroy()
	{
		if (colorRT != null) colorRT.Release();
		if (depthRT != null) depthRT.Release();
	}
}
