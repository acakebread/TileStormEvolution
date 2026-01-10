using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClassicTilestorm
{
	public class DefinitionEditorPanel : UIPanel
	{
		[Header("UI References")]
		[SerializeField] private Button closeButton;
		[SerializeField] private ScrollRect definitionScrollView;
		[SerializeField] private Transform contentParent;
		[SerializeField] private GameObject definitionListItemPrefab;
		[SerializeField] private RawImage previewImage;

		[Header("Preview Settings")]
		[SerializeField] private Vector2 previewResolution = new Vector2(320, 240); // fixed 4:3
		[SerializeField] private float cameraDistance = 5f;              // horizontal distance from origin
		[SerializeField] private float cameraHeight = 3f;                // vertical height above origin
		[SerializeField] private float cameraTiltAngle = 15f;            // downward look angle
		[SerializeField] private float cameraOrbitSpeed = 20f;           // degrees per second (0 = no orbit)
		[SerializeField] private Color backgroundColor = new Color(0.08f, 0.10f, 0.15f);

		private RenderTexture previewRenderTexture;
		private Camera previewCamera;
		private GameObject previewRoot;
		private GameObject currentModelInstance;
		private string selectedDefinitionId;

		private readonly List<GameObject> spawnedListItems = new();

		// Ground plane
		private Mesh groundMesh;
		private Material groundMat;

		private float orbitAngle = 0f; // accumulated rotation for camera orbit

		protected override void Awake()
		{
			base.Awake();

			if (closeButton != null)
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));

			if (contentParent == null)
				contentParent = definitionScrollView?.content;

			if (contentParent == null)
				Debug.LogError("Content parent not assigned!", this);
		}

		public override void OnPanelOpened()
		{
			base.OnPanelOpened();
			CleanupPreview();
			CreatePreviewSetup();
			RefreshDefinitionList();

			if (ResourceManager.Definitions.Count > 0)
				SelectDefinition(ResourceManager.Definitions[0].id);
		}

		public override void OnPanelClosed()
		{
			base.OnPanelClosed();
			CleanupPreview();
			ClearListItems();
		}

		private void Update()
		{
			if (previewCamera != null && previewRenderTexture != null)
			{
				previewCamera.Render();
			}

			// Orbit the camera around Y-axis (model & ground stay still)
			if (cameraOrbitSpeed > 0.01f)
			{
				orbitAngle += cameraOrbitSpeed * Time.deltaTime;
				UpdateCameraOrbit();
			}
		}

		protected override void OnDestroy()
		{
			CleanupPreview();
			base.OnDestroy();
		}

		// List population unchanged...

		private void RefreshDefinitionList()
		{
			ClearListItems();
			if (ResourceManager.Definitions.Count == 0) return;

			var vlg = contentParent.GetComponent<VerticalLayoutGroup>();
			var csf = contentParent.GetComponent<ContentSizeFitter>();
			bool vlgWas = vlg?.enabled ?? false;
			bool csfWas = csf?.enabled ?? false;

			if (vlg) vlg.enabled = false;
			if (csf) csf.enabled = false;

			foreach (var def in ResourceManager.Definitions)
				CreateDefinitionListItem(def);

			if (vlg) vlg.enabled = vlgWas;
			if (csf) csf.enabled = csfWas;

			LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent as RectTransform);
		}

		private void ClearListItems()
		{
			foreach (var item in spawnedListItems)
				if (item) Destroy(item);
			spawnedListItems.Clear();
		}

		private void CreateDefinitionListItem(Definition def)
		{
			if (!definitionListItemPrefab) return;

			var go = Instantiate(definitionListItemPrefab, contentParent);
			spawnedListItems.Add(go);

			var item = go.GetComponent<DefinitionListItem>();
			if (!item) return;

			item.Initialize(def.id, SelectDefinition);

			if (item.label)
			{
				item.label.richText = false;
				item.label.enableAutoSizing = false;
				item.label.fontSize = 20;
				item.label.text = $"{def.id} ({def.model ?? "—"})";
			}

			item.SetSelected(def.id == selectedDefinitionId);
		}

		private void SelectDefinition(string defId)
		{
			if (string.IsNullOrEmpty(defId)) return;
			selectedDefinitionId = defId;

			foreach (var go in spawnedListItems)
			{
				if (!go) continue;
				var item = go.GetComponent<DefinitionListItem>();
				if (item) item.SetSelected(item.DefinitionId == selectedDefinitionId);
			}

			UpdatePreview(defId);
		}

		// ── Preview Setup ──────────────────────────────────────────────────────

		private void CreatePreviewSetup()
		{
			if (!previewImage) return;

			previewRoot = new GameObject("DefinitionPreviewRoot");

			var camGO = new GameObject("PreviewCamera");
			previewCamera = camGO.AddComponent<Camera>();
			camGO.transform.SetParent(null); // Camera independent

			int editorLayer = LayerMask.NameToLayer("Editor");
			if (editorLayer == -1)
			{
				Debug.LogError("Layer 'Editor' not found!");
				return;
			}

			previewCamera.enabled = false;
			previewCamera.cullingMask = 1 << editorLayer;
			previewCamera.clearFlags = CameraClearFlags.SolidColor;
			previewCamera.backgroundColor = backgroundColor;
			previewCamera.orthographic = false;
			previewCamera.fieldOfView = 60f;
			previewCamera.nearClipPlane = 0.03f;
			previewCamera.farClipPlane = 50f;
			previewCamera.aspect = (float)previewResolution.x / previewResolution.y;

			previewRenderTexture = new RenderTexture(
				(int)previewResolution.x,
				(int)previewResolution.y,
				24, RenderTextureFormat.ARGB32);
			previewRenderTexture.Create();

			previewCamera.targetTexture = previewRenderTexture;
			previewImage.texture = previewRenderTexture;

			CreatePreviewGroundPlane();

			// Initial camera position
			UpdateCameraOrbit();
		}

		private void CreatePreviewGroundPlane()
		{
			groundMesh = new Mesh();
			groundMesh.vertices = new[]
			{
				new Vector3(-50, -0.02f, -50),
				new Vector3(-50, -0.02f,  50),
				new Vector3( 50, -0.02f,  50),
				new Vector3( 50, -0.02f, -50)
			};
			groundMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
			groundMesh.uv = new Vector2[]
			{
				new Vector2(0, 0),
				new Vector2(0, 10),
				new Vector2(10, 10),
				new Vector2(10, 0)
			};
			groundMesh.RecalculateNormals();

			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			groundMat = new Material(shader);
			groundMat.color = new Color(0.16f, 0.18f, 0.22f);

			var provider = previewRoot.AddComponent<SimpleCommandProvider>();
			provider.RegisterCommand(RenderPassEvent.BeforeRenderingOpaques, (cmd, cam) =>
			{
				if (!groundMesh || !groundMat) return;
				cmd.DrawMesh(groundMesh, Matrix4x4.identity, groundMat);
			});
		}

		private void UpdatePreview(string defId)
		{
			if (!previewCamera || string.IsNullOrEmpty(defId))
			{
				if (previewImage) previewImage.enabled = false;
				return;
			}

			if (previewImage) previewImage.enabled = true;

			if (currentModelInstance) Destroy(currentModelInstance);

			var def = ResourceManager.GetDefinition(defId);
			if (def == null || string.IsNullOrEmpty(def.model)) return;

			currentModelInstance = DefinitionFactory.Instantiate(def, parent: previewRoot.transform);
			if (!currentModelInstance)
			{
				Debug.LogError($"Failed to instantiate: {def.model}");
				return;
			}

			currentModelInstance.transform.localPosition = Vector3.zero;
			currentModelInstance.transform.localRotation = Quaternion.identity;

			SetLayerRecursively(currentModelInstance, LayerMask.NameToLayer("Editor"));

			// Reset orbit angle to start from front
			orbitAngle = 0f;
			UpdateCameraOrbit();
		}

		private void UpdateCameraOrbit()
		{
			if (!previewCamera) return;

			// Orbit around Y-axis at fixed distance/height
			float x = Mathf.Sin(orbitAngle * Mathf.Deg2Rad) * cameraDistance;
			float z = Mathf.Cos(orbitAngle * Mathf.Deg2Rad) * cameraDistance;

			previewCamera.transform.position = new Vector3(x, cameraHeight, z);

			// Look at origin (model center)
			previewCamera.transform.LookAt(Vector3.up * 1f);

			// Apply extra downward tilt if desired
			previewCamera.transform.Rotate(Vector3.right, cameraTiltAngle, Space.Self);
		}

		private void SetLayerRecursively(GameObject obj, int layer)
		{
			if (!obj) return;
			obj.layer = layer;
			foreach (Transform child in obj.transform)
				SetLayerRecursively(child.gameObject, layer);
		}

		private void CleanupPreview()
		{
			if (previewRenderTexture)
			{
				previewRenderTexture.Release();
				previewRenderTexture = null;
			}

			if (currentModelInstance)
			{
				Destroy(currentModelInstance);
				currentModelInstance = null;
			}

			if (groundMesh) Destroy(groundMesh);
			if (groundMat) Destroy(groundMat);

			if (previewRoot)
			{
				Destroy(previewRoot);
				previewRoot = null;
			}

			previewCamera = null;
			if (previewImage) previewImage.texture = null;
		}

		private class SimpleCommandProvider : MonoBehaviour, ICommandBufferProvider
		{
			private System.Action<RasterCommandBuffer, Camera> opaquesAction;

			public void RegisterCommand(RenderPassEvent evt, System.Action<RasterCommandBuffer, Camera> action)
			{
				if (evt == RenderPassEvent.BeforeRenderingOpaques)
					opaquesAction = action;
			}

			public bool HasCommands(RenderPassEvent evt) =>
				evt == RenderPassEvent.BeforeRenderingOpaques && opaquesAction != null;

			public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera cam)
			{
				if (evt == RenderPassEvent.BeforeRenderingOpaques)
					opaquesAction?.Invoke(cmd, cam);
			}
		}
	}
}