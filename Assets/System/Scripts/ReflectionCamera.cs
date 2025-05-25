using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ReflectionCamera : MonoBehaviour
{
	[SerializeField, Range(float.Epsilon, 1f)] private float brightness = 1f;
	[SerializeField] private Camera reflectionCamera; // Assign in Inspector or create programmatically
	[SerializeField] private bool compositeToScreen = true; // Set to true to composite to screen

	private Camera _mainCam;
	private RenderTexture _reflectionTexture;
	private RenderTexture _flippedTexture;
	private Light[] _sceneLights;
	private float[] _originalLightIntensities;
	private Color _originalAmbientLight;
	private float _originalAmbientIntensity;

	private void Start()
	{
		InitializeMainCamera();
		InitializeReflectionCamera();
		InitializeRenderTextures();
		InitializeSceneLights();
	}

	private void InitializeMainCamera()
	{
		_mainCam = GetComponent<Camera>();
		_mainCam.clearFlags = CameraClearFlags.Nothing; // Preserve reflection
		_mainCam.depth = 0; // Ensure main camera renders last
		Debug.Log("Main camera initialized: " + _mainCam.name);
	}

	private void InitializeReflectionCamera()
	{
		if (reflectionCamera == null)
		{
			var camObj = new GameObject("ReflectionCamera");
			reflectionCamera = camObj.AddComponent<Camera>();
		}

		reflectionCamera.enabled = false; // Render manually
		reflectionCamera.CopyFrom(_mainCam);
		reflectionCamera.depth = _mainCam.depth - 1; // Render before main camera
		reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
		reflectionCamera.backgroundColor = Color.clear;
		reflectionCamera.cullingMask = _mainCam.cullingMask; // Match main camera's culling
		reflectionCamera.nearClipPlane = _mainCam.nearClipPlane;
		reflectionCamera.farClipPlane = _mainCam.farClipPlane;
		reflectionCamera.fieldOfView = _mainCam.fieldOfView;
		Debug.Log("Reflection camera initialized: " + reflectionCamera.name);
	}

	private void InitializeRenderTextures()
	{
		_reflectionTexture = new RenderTexture(_mainCam.pixelWidth, _mainCam.pixelHeight, 24, RenderTextureFormat.ARGB32);
		_flippedTexture = new RenderTexture(_mainCam.pixelWidth, _mainCam.pixelHeight, 24, RenderTextureFormat.ARGB32);
		reflectionCamera.targetTexture = _reflectionTexture;
		Debug.Log("Render textures created: " + _reflectionTexture.name + ", " + _flippedTexture.name);
	}

	private void InitializeSceneLights()
	{
		_sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
		_originalLightIntensities = new float[_sceneLights.Length];
		for (var i = 0; i < _sceneLights.Length; i++)
		{
			_originalLightIntensities[i] = _sceneLights[i].intensity;
		}
		_originalAmbientLight = RenderSettings.ambientLight;
		_originalAmbientIntensity = RenderSettings.ambientIntensity;
		Debug.Log("Scene lights initialized: " + _sceneLights.Length + " lights found");
	}

	private void OnPreRender()
	{
		if (!reflectionCamera)
		{
			Debug.LogError("Reflection camera is null!");
			return;
		}

		// Sync reflection camera transform with main camera
		reflectionCamera.transform.SetPositionAndRotation(_mainCam.transform.position, _mainCam.transform.rotation);
		Debug.Log($"Reflection camera transform synced: {_mainCam.transform.position}");

		// Adjust lighting
		for (var i = 0; i < _sceneLights.Length; i++)
		{
			if (_sceneLights[i].enabled)
			{
				_sceneLights[i].intensity = _originalLightIntensities[i] * brightness;
			}
		}
		RenderSettings.ambientLight = _originalAmbientLight * brightness;
		RenderSettings.ambientIntensity = _originalAmbientIntensity * brightness;

		// Render reflection camera
		reflectionCamera.Render();
		Debug.Log("Reflection camera rendered to: " + _reflectionTexture.name);

		// Flip the RenderTexture vertically
		FlipRenderTexture();

		// Restore lighting
		for (var i = 0; i < _sceneLights.Length; i++)
		{
			if (_sceneLights[i].enabled)
			{
				_sceneLights[i].intensity = _originalLightIntensities[i];
			}
		}
		RenderSettings.ambientLight = _originalAmbientLight;
		RenderSettings.ambientIntensity = _originalAmbientIntensity;

		// Composite to screen if enabled
		if (compositeToScreen)
		{
			Graphics.Blit(_flippedTexture, null as RenderTexture);
			Debug.Log("Composited flipped texture to screen");
		}
	}

	private void FlipRenderTexture()
	{
		// Make the original RenderTexture readable
		RenderTexture.active = _reflectionTexture;
		Texture2D tempTexture = new Texture2D(_reflectionTexture.width, _reflectionTexture.height, TextureFormat.RGBA32, false);
		tempTexture.ReadPixels(new Rect(0, 0, _reflectionTexture.width, _reflectionTexture.height), 0, 0);
		tempTexture.Apply();
		Debug.Log("Read pixels from reflection texture");

		// Create flipped pixel data
		Color[] pixels = tempTexture.GetPixels();
		Color[] flippedPixels = new Color[pixels.Length];
		int width = _reflectionTexture.width;
		int height = _reflectionTexture.height;

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				flippedPixels[x + y * width] = pixels[x + (height - 1 - y) * width];
			}
		}
		Debug.Log($"Flipped {pixels.Length} pixels: {width}x{height}");

		// Apply flipped pixels to the flipped RenderTexture
		tempTexture.SetPixels(flippedPixels);
		tempTexture.Apply();
		RenderTexture.active = _flippedTexture;
		Graphics.CopyTexture(tempTexture, _flippedTexture);
		Debug.Log("Copied flipped pixels to flipped texture");

		// Cleanup
		Object.Destroy(tempTexture);
		RenderTexture.active = null;
	}

	private void OnDestroy()
	{
		if (_reflectionTexture != null)
		{
			_reflectionTexture.Release();
		}
		if (_flippedTexture != null)
		{
			_flippedTexture.Release();
		}
		if (reflectionCamera != null && reflectionCamera.gameObject != null)
		{
			Object.Destroy(reflectionCamera.gameObject);
		}
	}
}

//using UnityEngine;

//public class ReflectionCamera : MonoBehaviour
//{
//	public Camera reflectionCamera;
//	//public Material material;

//	void OnWillRenderObject()
//	{
//		if (reflectionCamera != null)// && material != null)
//		{
//			// Set up reflection camera
//			SetReflectionCamera();

//			// Render the scene through the reflection camera
//			reflectionCamera.Render();

//			// Apply the reflection texture to the material
//			//material.SetTexture("_DynReflTex", reflectionCamera.targetTexture);
//		}
//	}

//	private void SetReflectionCamera()
//	{
//		if (reflectionCamera != null)
//		{
//			// Calculate the reflection plane (Y-axis is inverted)
//			float distance = Vector3.Dot(transform.position, transform.up) * 2; //Y axis is inverted
//			reflectionCamera.transform.position = new Vector3(transform.position.x, distance + transform.position.y, transform.position.z);

//			// Set the view matrix
//			Vector3 cameraPosition = reflectionCamera.transform.position;
//			Quaternion rotation = Quaternion.Euler(180, 0, 0); // Rotate 180 degrees around X
//			//Matrix4x4 viewMatrix = Matrix4x4.LookAtMatrix(cameraPosition, transform.position, Vector3.Cross(transform.right, cameraPosition - transform.position).normalized);

//			//reflectionCamera.worldToCameraMatrix = viewMatrix; // Set the reflection camera's view matrix
//		}
//	}
//}