using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class MirroredPassRenderer : MonoBehaviour
{
	[SerializeField] private Vector3 planeNormal = Vector3.up;
	[SerializeField] private float offset;
	[SerializeField, Range(float.Epsilon, 1f)] private float brightness = 1f;

	private Camera _mainCam;
	private Camera _mirrorCam;
	private CommandBuffer _mirrorCommandBuffer;
	private CommandBuffer _mainCamCommandBuffer;
	private Light[] _sceneLights;
	private float[] _originalLightIntensities;
	private Color _originalAmbientLight;
	private float _originalAmbientIntensity;
	private CameraClearFlags _originalCameraClearFlags;
	private Material _originalSkyboxMaterial;
	private Material _flippedSkyboxMaterial;

	private void Start()
	{
		InitializeMainCamera();
		InitializeMirrorCamera();
		InitializeCommandBuffers();
		InitializeSceneLights();
		InitializeSkybox();
	}

	private void InitializeMainCamera()
	{
		_mainCam = Camera.main;
		_originalCameraClearFlags = _mainCam.clearFlags;
		_mainCam.clearFlags = CameraClearFlags.Nothing; // Required for reflection
	}

	private void InitializeMirrorCamera()
	{
		var camObj = new GameObject("MirrorCamera") { hideFlags = HideFlags.HideAndDontSave };
		_mirrorCam = camObj.AddComponent<Camera>();
		_mirrorCam.enabled = false;
	}

	private void InitializeCommandBuffers()
	{
		_mirrorCommandBuffer = new CommandBuffer { name = "MirrorCameraCullingFix" };
		_mirrorCam.AddCommandBuffer(CameraEvent.BeforeDepthTexture, _mirrorCommandBuffer);

		_mainCamCommandBuffer = new CommandBuffer { name = "MainCameraCullingReset" };
		_mainCamCommandBuffer.SetInvertCulling(false);
		_mainCam.AddCommandBuffer(CameraEvent.BeforeDepthTexture, _mainCamCommandBuffer);
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
	}

	private void InitializeSkybox()
	{
		if (RenderSettings.skybox == null)
		{
			Debug.LogWarning("No skybox material set in RenderSettings!");
			return;
		}

		_originalSkyboxMaterial = RenderSettings.skybox;
		_flippedSkyboxMaterial = new Material(_originalSkyboxMaterial) { name = "FlippedNightskySkybox" };

		// Swap top and bottom textures
		SwapTextures("_UpTex", "_DownTex");

		// Flip all textures vertically
		FlipAllTextures();

		RenderSettings.skybox = _flippedSkyboxMaterial;
	}

	private void SwapTextures(string texProperty1, string texProperty2)
	{
		if (!_flippedSkyboxMaterial.HasProperty(texProperty1) || !_flippedSkyboxMaterial.HasProperty(texProperty2))
		{
			Debug.LogWarning($"Skybox shader lacks properties: {texProperty1}, {texProperty2}");
			return;
		}

		var tex1 = _flippedSkyboxMaterial.GetTexture(texProperty1);
		var tex2 = _flippedSkyboxMaterial.GetTexture(texProperty2);
		if (tex1 == null || tex2 == null)
		{
			Debug.LogWarning($"Textures not found: {texProperty1}, {texProperty2}");
			return;
		}

		_flippedSkyboxMaterial.SetTexture(texProperty1, tex2);
		_flippedSkyboxMaterial.SetTexture(texProperty2, tex1);
	}

	private void FlipAllTextures()
	{
		var textureProperties = new[] { "_UpTex", "_DownTex", "_FrontTex", "_BackTex", "_LeftTex", "_RightTex" };
		foreach (var prop in textureProperties)
		{
			if (!_flippedSkyboxMaterial.HasProperty(prop)) continue;

			var texture = _flippedSkyboxMaterial.GetTexture(prop);
			if (texture == null)
			{
				Debug.LogWarning($"Texture not found for {prop}");
				continue;
			}

			var flippedTexture = FlipTextureViaGPU(texture);
			flippedTexture.filterMode = FilterMode.Trilinear;
			flippedTexture.wrapMode = TextureWrapMode.Clamp;
			_flippedSkyboxMaterial.SetTexture(prop, flippedTexture);
		}
	}

	private static Texture2D FlipTextureViaGPU(Texture originalTex)
	{
		var width = originalTex.width;
		var height = originalTex.height;

		var rt = new RenderTexture(width, height, 0);
		RenderTexture.active = rt;
		Graphics.Blit(originalTex, rt);

		var flipped = new Texture2D(width, height, TextureFormat.RGBA32, false);
		flipped.ReadPixels(new Rect(0, 0, width, height), 0, 0);
		flipped.Apply();

		RenderTexture.active = null;
		rt.Release();

		return FlipTextureVertically(flipped);
	}

	private static Texture2D FlipTextureVertically(Texture2D original)
	{
		var width = original.width;
		var height = original.height;
		var flipped = new Texture2D(width, height, original.format, false)
		{
			wrapMode = original.wrapMode,
			filterMode = original.filterMode
		};

		for (var y = 0; y < height; y++)
		{
			flipped.SetPixels(0, y, width, 1, original.GetPixels(0, height - 1 - y, width, 1));
		}

		flipped.Apply();
		return flipped;
	}

	private void OnPreRender()
	{
		if (!_mainCam || !_mirrorCam) return;

		_mirrorCam.CopyFrom(_mainCam);
		_mirrorCam.clearFlags = _originalCameraClearFlags;
		_mirrorCam.depth = _mainCam.depth - 1;

		var normalizedNormal = planeNormal.normalized;
		var pointOnPlane = normalizedNormal * offset;

		var reflectionMat = Matrix4x4.identity;
		reflectionMat[0, 0] = 1 - 2 * normalizedNormal.x * normalizedNormal.x;
		reflectionMat[0, 1] = -2 * normalizedNormal.x * normalizedNormal.y;
		reflectionMat[0, 2] = -2 * normalizedNormal.x * normalizedNormal.z;
		reflectionMat[1, 0] = -2 * normalizedNormal.y * normalizedNormal.x;
		reflectionMat[1, 1] = 1 - 2 * normalizedNormal.y * normalizedNormal.y;
		reflectionMat[1, 2] = -2 * normalizedNormal.y * normalizedNormal.z;
		reflectionMat[2, 0] = -2 * normalizedNormal.z * normalizedNormal.x;
		reflectionMat[2, 1] = -2 * normalizedNormal.z * normalizedNormal.y;
		reflectionMat[2, 2] = 1 - 2 * normalizedNormal.z * normalizedNormal.z;

		var translateToOrigin = Matrix4x4.Translate(-pointOnPlane);
		var translateBack = Matrix4x4.Translate(pointOnPlane);
		reflectionMat = translateBack * reflectionMat * translateToOrigin;

		_mirrorCam.worldToCameraMatrix = _mainCam.worldToCameraMatrix * reflectionMat;
		_mirrorCam.rect = new Rect(0, 0, 1, 1);

		for (var i = 0; i < _sceneLights.Length; i++)
		{
			if (_sceneLights[i].enabled)
			{
				_sceneLights[i].intensity = _originalLightIntensities[i] * brightness;
			}
		}

		RenderSettings.ambientLight = _originalAmbientLight * brightness;
		RenderSettings.ambientIntensity = _originalAmbientIntensity * brightness;

		_mirrorCommandBuffer.Clear();
		_mirrorCommandBuffer.SetInvertCulling(true);

		_mirrorCam.Render();

		for (var i = 0; i < _sceneLights.Length; i++)
		{
			if (_sceneLights[i].enabled)
			{
				_sceneLights[i].intensity = _originalLightIntensities[i];
			}
		}

		RenderSettings.ambientLight = _originalAmbientLight;
		RenderSettings.ambientIntensity = _originalAmbientIntensity;

		_mirrorCommandBuffer.SetInvertCulling(false);
	}

	private void OnDestroy()
	{
		if (_mirrorCam != null)
		{
			if (_mirrorCommandBuffer != null)
			{
				_mirrorCam.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, _mirrorCommandBuffer);
				_mirrorCommandBuffer.Release();
			}
			Destroy(_mirrorCam.gameObject);
		}

		if (_mainCam != null && _mainCamCommandBuffer != null)
		{
			_mainCam.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, _mainCamCommandBuffer);
			_mainCamCommandBuffer.Release();
		}

		if (_originalSkyboxMaterial != null)
		{
			RenderSettings.skybox = _originalSkyboxMaterial;
		}

		if (_flippedSkyboxMaterial != null)
		{
			Destroy(_flippedSkyboxMaterial);
		}
	}
}