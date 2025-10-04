using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(MeshRenderer))]
public class FrostedQuad : MonoBehaviour
{
	[SerializeField, Range(1, 120)] private float frostRadius = 32f;
	[SerializeField] private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 1f);
	[SerializeField] private LayerMask cullingMask = ~0; // Layers to render (exclude quad’s layer)
	[SerializeField] private Texture2D noiseTexture; // Optional noise texture
	//[SerializeField] private RenderTexture blurTexture;

	private RenderTexture renderTexture;
	private Material frostedMaterial;
	private MeshRenderer meshRenderer;
	private Camera textureCamera;

	void Awake()
	{
		meshRenderer = GetComponent<MeshRenderer>();
		if (meshRenderer == null)
		{
			Debug.LogError("FrostedQuad: MeshRenderer component missing!", this);
			enabled = false;
			return;
		}

		// Set quad to a unique layer (e.g., UI)
		gameObject.layer = LayerMask.NameToLayer("UI"); // Ensure this layer is excluded from cullingMask

		// Create render texture (optimized for WebGL)
		int width = Screen.width; // Use screen resolution for WebGL
		int height = Screen.height;
		renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
		renderTexture.filterMode = FilterMode.Bilinear; // Bilinear for WebGL compatibility
		renderTexture.useDynamicScale = true; // Auto-scale for low-memory devices
		renderTexture.Create();

		//blurTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
		//blurTexture.filterMode = FilterMode.Bilinear; // Bilinear for WebGL compatibility
		//blurTexture.useDynamicScale = true; // Auto-scale for low-memory devices
		//blurTexture.Create();       
		
		// Create frosted material
		var frostedShader = Shader.Find("Unlit/URPFrosted");
		if (!frostedShader)
		{
			Debug.LogError("FrostedQuad: Unlit/URPFrosted shader not found!", this);
			enabled = false;
			return;
		}

		frostedMaterial = new Material(frostedShader);
		frostedMaterial.SetColor("_BaseColor", baseColor);
		frostedMaterial.SetFloat("_Radius", frostRadius);
		frostedMaterial.SetTexture("_MainTex", renderTexture);
		frostedMaterial.SetTexture("_NoiseTex", noiseTexture);
		//frostedMaterial.SetTexture("_BlurTex", blurTexture);
		frostedMaterial.SetFloat("_NoiseStrength", 0.02f);
		meshRenderer.material = frostedMaterial;

		// Create and configure texture camera
		GameObject cameraObj = new GameObject("FrostedTextureCamera") { hideFlags = HideFlags.HideInHierarchy };
		cameraObj.transform.SetParent(transform, false);
		textureCamera = cameraObj.AddComponent<Camera>();
		textureCamera.CopyFrom(Camera.main);
		textureCamera.cullingMask = (cullingMask & ~(1 << gameObject.layer)) | (1 << LayerMask.NameToLayer("Default")); // Include Default layer
		textureCamera.clearFlags = CameraClearFlags.Skybox;
		textureCamera.depth = -1;
		textureCamera.targetTexture = renderTexture;
		textureCamera.enabled = true;

		var data = cameraObj.AddComponent<UniversalAdditionalCameraData>();
		data.renderType = CameraRenderType.Base;

		Debug.Log($"FrostedQuad: Initialized with shader={frostedMaterial.shader.name}, renderTexture={renderTexture.name}, resolution={width}x{height}");
	}

	void Update()
	{
		if (frostedMaterial == null || textureCamera == null || Camera.main == null)
			return;

		// Update material properties
		frostedMaterial.SetFloat("_Radius", frostRadius);
		frostedMaterial.SetColor("_BaseColor", baseColor);

		// Sync texture camera with main camera
		textureCamera.fieldOfView = Camera.main.fieldOfView;
		textureCamera.nearClipPlane = Camera.main.nearClipPlane;
		textureCamera.farClipPlane = Camera.main.farClipPlane;
		textureCamera.aspect = Camera.main.aspect;
		textureCamera.orthographic = Camera.main.orthographic;
		textureCamera.orthographicSize = Camera.main.orthographicSize;
		textureCamera.transform.position = Camera.main.transform.position;
		textureCamera.transform.rotation = Camera.main.transform.rotation;
	}

	void OnDestroy()
	{
		if (frostedMaterial != null)
			DestroyImmediate(frostedMaterial);
		if (textureCamera != null)
			textureCamera.targetTexture = null;
		if (textureCamera != null)
			DestroyImmediate(textureCamera.gameObject);
		if (renderTexture != null)
			DestroyImmediate(renderTexture);
		//if (blurTexture != null)
		//	DestroyImmediate(blurTexture);
	}
}