using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera), typeof(CommandBufferSettings))]
public class ReflectionPassCamera : MonoBehaviour
{
	[SerializeField] private Vector3 planeNormal = Vector3.up; // Reflection plane normal
	[SerializeField] private float offset = -0.2f; // Reflection plane offset
	[SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.5f); // Dim overlay color
	[SerializeField] private Material dimMaterial; // Custom/UnlitFixedColor or URP Unlit
	[HideInInspector] public LayerMask sceneCullingMask = ~0; // Culling mask for reflection and scene cameras

	private Camera mainCamera; // Skybox camera (this GameObject, Base, MainCamera tag)
	private Camera reflectionCamera; // Child Reflection camera (Overlay)
	private Camera sceneCamera; // Child Scene camera (Overlay)
	private CommandBufferSettings commandBufferSettings;
	private bool isDimMaterialDynamic; // Track if dimMaterial was created dynamically

	void Awake()
	{
		// Initialize main camera (Skybox, Base)
		mainCamera = GetComponent<Camera>();
		if (mainCamera == null)
		{
			Debug.LogError("Main camera component missing!", this);
			enabled = false;
			return;
		}

		// Ensure MainCamera tag
		if (gameObject.tag != "MainCamera")
		{
			gameObject.tag = "MainCamera";
			Debug.Log("Set MainCamera tag on Skybox camera", this);
		}

		// Configure main camera
		sceneCullingMask = mainCamera.cullingMask;
		mainCamera.clearFlags = CameraClearFlags.Skybox;
		mainCamera.cullingMask = 0; // Nothing, as skybox is rendered by material
		mainCamera.depth = -2;
		mainCamera.enabled = true;

		// Get CommandBufferSettings
		commandBufferSettings = GetComponent<CommandBufferSettings>();
		if (commandBufferSettings == null)
		{
			Debug.LogError("CommandBufferSettings component missing!", this);
			enabled = false;
			return;
		}
		Debug.Log("CommandBufferSettings initialized", this);

		// Create child cameras
		InitializeReflectionCamera();
		InitializeSceneCamera();

		// Configure URP camera stack (for compatibility)
		ConfigureCameraStack();

		Debug.Log("ReflectionPassCamera initialized successfully", this);
	}

	private void InitializeReflectionCamera()
	{
		var reflectionObj = new GameObject("ReflectionCamera");// { hideFlags = HideFlags.HideAndDontSave };
		reflectionObj.transform.SetParent(transform, false);
		reflectionCamera = reflectionObj.AddComponent<Camera>();
		SyncCameraProperties(reflectionCamera);
		reflectionCamera.clearFlags = CameraClearFlags.Depth;
		reflectionCamera.cullingMask = sceneCullingMask;
		reflectionCamera.depth = -1;
		reflectionCamera.enabled = true; // Required for manual rendering

		// Add ReflectionCamera component
		var reflection = reflectionObj.AddComponent<ReflectionCamera>();
		reflection.planeNormal = planeNormal;
		reflection.offset = offset;
		// Set fields via reflection before Awake runs
		System.Reflection.FieldInfo referenceCameraField = typeof(ReflectionCamera).GetField("referenceCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		if (referenceCameraField != null)
		{
			referenceCameraField.SetValue(reflection, mainCamera);
			Debug.Log($"Set referenceCamera to {mainCamera} in ReflectionCamera", this);
		}
		else
		{
			Debug.LogError("Field referenceCamera not found in ReflectionCamera", this);
		}
		Debug.Log("Initialized ReflectionCamera", this);

		// Add UniversalAdditionalCameraData
		var reflectionData = reflectionObj.AddComponent<UniversalAdditionalCameraData>();
		reflectionData.renderType = CameraRenderType.Overlay;
	}

	private void InitializeSceneCamera()
	{
		var sceneObj = new GameObject("SceneCamera");// { hideFlags = HideFlags.HideAndDontSave };
		sceneObj.transform.SetParent(transform, false);
		sceneCamera = sceneObj.AddComponent<Camera>();
		SyncCameraProperties(sceneCamera);
		sceneCamera.clearFlags = CameraClearFlags.Nothing;
		sceneCamera.cullingMask = sceneCullingMask;
		sceneCamera.depth = 0;
		sceneCamera.enabled = true; // Required for manual rendering

		// Add DimOverlay component
		var dimOverlay = sceneObj.AddComponent<DimOverlay>();
		if (dimMaterial == null)
		{
			Shader unlitShader = Shader.Find("Custom/UnlitFixedColor") ?? Shader.Find("Universal Render Pipeline/Unlit");
			if (unlitShader == null)
			{
				Debug.LogError("Failed to find Unlit shader!", this);
				enabled = false;
				return;
			}
			dimMaterial = new Material(unlitShader);
			dimMaterial.SetFloat("_Surface", 1.0f); // Transparent
			dimMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
			dimMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
			dimMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.SrcAlpha);
			dimMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
			dimMaterial.SetFloat("_ZWrite", 0.0f);
			dimMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
			dimMaterial.SetFloat("_AlphaClip", 0.0f);
			dimMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			dimMaterial.renderQueue = (int)RenderQueue.Transparent;
			isDimMaterialDynamic = true;
			Debug.Log("Created dynamic dimMaterial", this);
		}
		// Set fields via reflection before Awake runs
		System.Reflection.FieldInfo sceneCameraField = typeof(DimOverlay).GetField("sceneCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		System.Reflection.FieldInfo reflectionCameraField = typeof(DimOverlay).GetField("reflectionCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		System.Reflection.FieldInfo dimColorField = typeof(DimOverlay).GetField("dimColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		System.Reflection.FieldInfo dimMaterialField = typeof(DimOverlay).GetField("dimMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		if (sceneCameraField != null) sceneCameraField.SetValue(dimOverlay, sceneCamera);
		if (reflectionCameraField != null) reflectionCameraField.SetValue(dimOverlay, reflectionCamera);
		if (dimColorField != null) dimColorField.SetValue(dimOverlay, dimColor);
		if (dimMaterialField != null) dimMaterialField.SetValue(dimOverlay, dimMaterial);
		Debug.Log($"Set DimOverlay fields: sceneCamera={sceneCamera}, reflectionCamera={reflectionCamera}, dimColor={dimColor}, dimMaterial={dimMaterial}", this);
		Debug.Log("Initialized SceneCamera with DimOverlay", this);

		// Add UniversalAdditionalCameraData
		var sceneData = sceneObj.AddComponent<UniversalAdditionalCameraData>();
		sceneData.renderType = CameraRenderType.Overlay;
	}

	private void SyncCameraProperties(Camera targetCamera)
	{
		targetCamera.fieldOfView = mainCamera.fieldOfView;
		targetCamera.nearClipPlane = mainCamera.nearClipPlane;
		targetCamera.farClipPlane = mainCamera.farClipPlane;
		targetCamera.transform.position = mainCamera.transform.position;
		targetCamera.transform.rotation = mainCamera.transform.rotation;
		targetCamera.aspect = mainCamera.aspect;
		targetCamera.orthographic = mainCamera.orthographic;
		targetCamera.orthographicSize = mainCamera.orthographicSize;
	}

	private void ConfigureCameraStack()
	{
		var mainCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
		if (mainCameraData == null)
		{
			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
			Debug.Log("Added UniversalAdditionalCameraData to MainCamera", this);
		}
		mainCameraData.renderType = CameraRenderType.Base;
		mainCameraData.cameraStack.Clear();
		if (reflectionCamera != null)
		{
			mainCameraData.cameraStack.Add(reflectionCamera);
			Debug.Log("Added ReflectionCamera to stack", this);
		}
		if (sceneCamera != null)
		{
			mainCameraData.cameraStack.Add(sceneCamera);
			Debug.Log("Added SceneCamera to stack", this);
		}
		Debug.Log($"Camera stack configured: {mainCameraData.cameraStack.Count} cameras", this);
	}

	private void SetPrivateField(object target, string fieldName, object value)
	{
		var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		if (field != null)
		{
			field.SetValue(target, value);
			//Debug.Log($"Set {fieldName} to {value} in {target.GetType().Name}", this);
		}
		else
		{
			Debug.LogError($"Field {fieldName} not found in {target.GetType().Name}", this);
		}
	}

	void LateUpdate()
	{
		// Sync camera properties
		//if (reflectionCamera != null)// copy is not currently needed because the reflection camera successfully copies properties in time
		//{
		//	SyncCameraProperties(reflectionCamera);
		//	var reflection = reflectionCamera.GetComponent<ReflectionCamera>();
		//	if (reflection != null)
		//	{
		//		reflection.planeNormal = planeNormal;
		//		reflection.offset = offset;
		//		SetPrivateField(reflection, "referenceCamera", mainCamera);
		//		reflection.enabled = true;
		//	}
		//}
		if (sceneCamera != null)
		{
			SyncCameraProperties(sceneCamera);
			var dimOverlay = sceneCamera.GetComponent<DimOverlay>();
			if (dimOverlay != null)
			{
				SetPrivateField(dimOverlay, "sceneCamera", sceneCamera);
				SetPrivateField(dimOverlay, "reflectionCamera", reflectionCamera);
				SetPrivateField(dimOverlay, "dimColor", dimColor);
				SetPrivateField(dimOverlay, "dimMaterial", dimMaterial);
				dimOverlay.enabled = true;
			}
		}
	}

	void OnDestroy()
	{
		if (reflectionCamera != null)
		{
			DestroyImmediate(reflectionCamera.gameObject, true);
		}
		if (sceneCamera != null)
		{
			DestroyImmediate(sceneCamera.gameObject, true);
		}
		if (dimMaterial != null && isDimMaterialDynamic)
		{
			DestroyImmediate(dimMaterial, true);
			Debug.Log("Destroyed dynamic dimMaterial", this);
		}
	}
}