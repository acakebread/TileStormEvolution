using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class ReflectionCamera : MonoBehaviour
{
	[SerializeField] private Camera referenceCamera; // Overlay camera (Scene Camera)
	[SerializeField] private Vector3 planeNormal = Vector3.up;
	[SerializeField] private float offset;
	[SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.7f); // Dark grey with alpha

	private Camera reflectionCamera;
	private CommandBuffer cullingCommandBuffer;
	private CommandBuffer resetCullingBuffer;
	private GameObject dimPlane;
	private Material dimMaterial; // Store material for real-time updates
	private Color lastDimColor; // Track last color to detect changes

	void Awake()
	{
		reflectionCamera = GetComponent<Camera>();
		reflectionCamera.clearFlags = CameraClearFlags.Depth; // Skybox handled by Base camera

		if (referenceCamera == null)
		{
			return;
		}

		// Create CommandBuffer for culling
		cullingCommandBuffer = new CommandBuffer { name = "ReflectionCulling" };
		cullingCommandBuffer.SetInvertCulling(true);
		reflectionCamera.AddCommandBuffer(CameraEvent.BeforeDepthTexture, cullingCommandBuffer);

		// Create reset CommandBuffer
		resetCullingBuffer = new CommandBuffer { name = "ResetCulling" };
		resetCullingBuffer.SetInvertCulling(false);
		reflectionCamera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, resetCullingBuffer);

		// Create dimming plane
		CreateDimPlane();
	}

	void CreateDimPlane()
	{
		// Create quad
		dimPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
		dimPlane.name = "ReflectionDimPlane";
		dimPlane.hideFlags = HideFlags.HideInHierarchy; // Hide in Inspector/Hierarchy

		// Position and orient at reflection plane
		var normalizedNormal = planeNormal.normalized;
		dimPlane.transform.position = normalizedNormal * offset;
		dimPlane.transform.rotation = Quaternion.LookRotation(normalizedNormal); // Face upward for Vector3.up
		dimPlane.transform.localScale = Vector3.one * 100000f; // Large enough to cover view

		// Set layer
		int reflectionDimLayer = LayerMask.NameToLayer("ReflectionDim");
		if (reflectionDimLayer == -1)
		{
			return;
		}
		dimPlane.layer = reflectionDimLayer;

		// Set culling mask
		reflectionCamera.cullingMask = (1 << reflectionDimLayer) | (1 << LayerMask.NameToLayer("Default"));

		// Create material
		dimMaterial = new Material(Shader.Find("Unlit/UnlitTransparentDim"));
		dimMaterial.SetColor("_BaseColor", dimColor); // Use dimColor with its alpha
		dimMaterial.renderQueue = 3100; // Transparent queue
		dimMaterial.EnableKeyword("_ALPHABLEND_ON");
		dimMaterial.SetFloat("_ZWrite", 0);
		dimMaterial.SetFloat("_Surface", 1); // Transparent
		dimMaterial.SetFloat("_Blend", 0); // Alpha blend

		// Assign material and force update
		var renderer = dimPlane.GetComponent<MeshRenderer>();
		renderer.material = dimMaterial;
		renderer.enabled = false;
		renderer.enabled = true; // Force renderer update

		// Initialize last color
		lastDimColor = dimColor;
	}

	void OnValidate()
	{
		// Update material color in Edit mode when dimColor changes in Inspector
		if (dimMaterial != null && dimColor != lastDimColor)
		{
			dimMaterial.SetColor("_BaseColor", dimColor);
			lastDimColor = dimColor;
		}
	}

	void Update()
	{
		// Update material color in Play mode when dimColor changes in Inspector
		if (dimMaterial != null && dimColor != lastDimColor)
		{
			dimMaterial.SetColor("_BaseColor", dimColor);
			lastDimColor = dimColor;
		}
	}

	void OnRenderObject()
	{
		if (referenceCamera == null || reflectionCamera == null)
		{
			return;
		}

		// Copy properties from the Reference Camera (Scene Camera)
		reflectionCamera.fieldOfView = referenceCamera.fieldOfView;
		reflectionCamera.nearClipPlane = referenceCamera.nearClipPlane;
		reflectionCamera.farClipPlane = referenceCamera.farClipPlane;
		reflectionCamera.orthographic = referenceCamera.orthographic;
		reflectionCamera.orthographicSize = referenceCamera.orthographicSize;
		reflectionCamera.aspect = referenceCamera.aspect;
		reflectionCamera.rect = referenceCamera.rect;

		// Compute reflection matrix
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

		// Apply reflection matrix
		reflectionCamera.worldToCameraMatrix = referenceCamera.worldToCameraMatrix * reflectionMat;
		reflectionCamera.projectionMatrix = referenceCamera.projectionMatrix;

		// Force culling CommandBuffer execution
		Graphics.ExecuteCommandBuffer(cullingCommandBuffer);
	}

	void OnDisable()
	{
		if (cullingCommandBuffer != null)
		{
			reflectionCamera.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, cullingCommandBuffer);
			cullingCommandBuffer.Release();
		}
		if (resetCullingBuffer != null)
		{
			reflectionCamera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, resetCullingBuffer);
			resetCullingBuffer.Release();
		}
		if (dimPlane != null)
		{
			Destroy(dimPlane);
		}
	}
}