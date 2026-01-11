using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using MassiveHadronLtd;

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
		[SerializeField] private Vector2 previewResolution = new Vector2(320, 240);
		[SerializeField] private float cameraDistance = 5f;
		[SerializeField] private float cameraHeight = 3f;
		[SerializeField] private float cameraTiltAngle = 15f;
		[SerializeField] private float cameraOrbitSpeed = 0f;
		[SerializeField] private Color backgroundColor = new Color(0f, 0.33f, 1f);

		[Header("Preview Ground Plane")]
		[SerializeField] private Color groundColor = new Color(1f, 1f, 1f);
		[SerializeField] private float groundSize = 8f;           // half-extent
		[SerializeField] private float groundY = -0.02f;
		[SerializeField] private float groundUVScale = 10f;

		private RenderTexture previewRenderTexture;
		private Camera previewCamera;
		private GameObject previewRoot;
		private GameObject currentModelInstance;
		private string selectedDefinitionId;

		private readonly List<GameObject> spawnedListItems = new();

		// Ground plane resources
		private Mesh groundMesh;
		private Material groundMat;
		private Texture2D groundTex;

		private float orbitAngle = 0f;

		private RectTransform previewRaycastBlocker;

		// ─────────────────────────────────────────────────────────────

		protected override void Awake()
		{
			base.Awake();

			if (closeButton != null)
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));

			if (contentParent == null)
				contentParent = definitionScrollView?.content;
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
			// This is crucial - we need to render the camera every frame
			// so the CommandBufferFeature can execute our ICommandBufferProvider commands
			if (previewCamera != null && previewRenderTexture != null && previewRenderTexture.IsCreated())
			{
				previewCamera.Render();
			}

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

		// ─────────────────────────── LIST ───────────────────────────

		private void RefreshDefinitionList()
		{
			ClearListItems();
			if (ResourceManager.Definitions.Count == 0) return;

			foreach (var def in ResourceManager.Definitions)
				CreateDefinitionListItem(def);
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

		// ──────────────────────── PREVIEW SETUP ─────────────────────────

		private void CreatePreviewSetup()
		{
			if (!previewImage) return;

			previewRoot = new GameObject("DefinitionPreviewRoot");

			var camGO = new GameObject("PreviewCamera");
			previewCamera = camGO.AddComponent<Camera>();
			camGO.transform.SetParent(previewRoot.transform);

			int editorLayer = LayerMask.NameToLayer("Editor");
			if (editorLayer == -1)
			{
				Debug.LogError("Layer 'Editor' not found!");
				return;
			}

			previewCamera.enabled = false;
			previewCamera.cullingMask = 0;// 1 << editorLayer;
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
				24,
				RenderTextureFormat.ARGB32);

			previewRenderTexture.Create();
			previewCamera.targetTexture = previewRenderTexture;
			previewImage.texture = previewRenderTexture;

			CreateGroundPlane();
			AttachCommandProvider(camGO);

			CreatePreviewRaycastBlocker();

			UpdateCameraOrbit();
		}

		private void CreateGroundPlane()
		{
			// Mesh
			groundMesh = new Mesh { name = "PreviewGroundMesh" };

			float half = groundSize;
			groundMesh.vertices = new[]
			{
				new Vector3(-half, groundY, -half),
				new Vector3(-half, groundY,  half),
				new Vector3( half, groundY,  half),
				new Vector3( half, groundY, -half),
			};

			groundMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };

			groundMesh.uv = new[]
			{
				new Vector2(0, 0),
				new Vector2(0, groundUVScale),
				new Vector2(groundUVScale, groundUVScale),
				new Vector2(groundUVScale, 0)
			};

			groundMesh.RecalculateNormals();

			// Texture (placeholder - replace later with real one)
			groundTex = TextureUtils.GenerateXorTexture256();

			// Material
			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
			{
				Debug.LogError("URP Unlit shader not found!");
				return;
			}

			groundMat = new Material(shader)
			{
				name = "PreviewGroundMat",
				hideFlags = HideFlags.HideAndDontSave
			};

			groundMat.SetFloat("_Surface", 0f);
			groundMat.SetTexture("_BaseMap", groundTex);
			groundMat.SetColor("_BaseColor", groundColor);
		}

		private void AttachCommandProvider(GameObject camGO)
		{
			var provider = camGO.AddComponent<CommandBufferProvider>();

			provider.RegisterCommand(
				RenderPassEvent.BeforeRenderingOpaques,
				(cmd, cam) =>
				{
					if (groundMesh == null || groundMat == null) return;
					cmd.DrawMesh(groundMesh, Matrix4x4.identity, groundMat, 0, 0);
				});

			Debug.Log("[DefinitionPreview] Command provider attached - ground should appear when camera renders");
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

			orbitAngle = 0f;
			UpdateCameraOrbit();
		}

		private void UpdateCameraOrbit()
		{
			if (!previewCamera) return;

			float x = Mathf.Sin(orbitAngle * Mathf.Deg2Rad) * cameraDistance;
			float z = Mathf.Cos(orbitAngle * Mathf.Deg2Rad) * cameraDistance;

			previewCamera.transform.position = new Vector3(x, cameraHeight, z);
			previewCamera.transform.LookAt(Vector3.up * 1f);
			previewCamera.transform.Rotate(Vector3.right, cameraTiltAngle, Space.Self);
		}

		public void DragPreviewCamera(Vector2 delta)
		{
			orbitAngle += delta.x * 0.25f;
			cameraHeight -= delta.y * 0.02f;
			cameraHeight = Mathf.Clamp(cameraHeight, 0.5f, 10f);
			UpdateCameraOrbit();
		}

		public void ZoomPreviewCamera(float scroll)
		{
			cameraDistance -= scroll * 0.3f;
			cameraDistance = Mathf.Clamp(cameraDistance, 1f, 20f);
			UpdateCameraOrbit();
		}

		private void SetLayerRecursively(GameObject obj, int layer)
		{
			if (!obj) return;
			obj.layer = layer;
			foreach (Transform child in obj.transform)
				SetLayerRecursively(child.gameObject, layer);
		}

		private void CreatePreviewRaycastBlocker()
		{
			if (previewRaycastBlocker || !previewImage) return;

			GameObject blocker = new GameObject("PreviewRaycastBlocker");
			blocker.transform.SetParent(previewImage.transform, false);

			var rt = blocker.AddComponent<RectTransform>();
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;

			var img = blocker.AddComponent<Image>();
			img.color = new Color(0, 0, 0, 0);
			img.raycastTarget = true;

			blocker.AddComponent<PreviewCameraInput>();
			previewRaycastBlocker = rt;
		}

		private void CleanupPreview()
		{
			if (previewRenderTexture != null)
			{
				previewRenderTexture.Release();
				previewRenderTexture = null;
			}

			if (currentModelInstance)
			{
				Destroy(currentModelInstance);
				currentModelInstance = null;
			}

			// Ground cleanup
			if (groundMesh) DestroyImmediate(groundMesh);
			if (groundMat) DestroyImmediate(groundMat);
			if (groundTex) DestroyImmediate(groundTex);

			groundMesh = null;
			groundMat = null;
			groundTex = null;

			if (previewRoot)
			{
				Destroy(previewRoot);
				previewRoot = null;
			}

			previewCamera = null;
			if (previewImage) previewImage.texture = null;
		}

		// ───────────────────── CAMERA INPUT ─────────────────────

		private class PreviewCameraInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
		{
			private DefinitionEditorPanel panel;
			private Vector2 last;

			private void Awake()
			{
				panel = GetComponentInParent<DefinitionEditorPanel>();
			}

			public void OnPointerDown(PointerEventData e)
			{
				last = e.position;
			}

			public void OnDrag(PointerEventData e)
			{
				Vector2 delta = e.position - last;
				last = e.position;
				panel.DragPreviewCamera(delta);
			}

			public void OnScroll(PointerEventData e)
			{
				panel.ZoomPreviewCamera(e.scrollDelta.y);
			}
		}

		// ───────────────────── COMMAND BUFFER PROVIDER ─────────────────────

		private class CommandBufferProvider : MonoBehaviour, ICommandBufferProvider
		{
			private readonly Dictionary<RenderPassEvent, System.Action<RasterCommandBuffer, Camera>> commands = new();

			public void RegisterCommand(RenderPassEvent evt, System.Action<RasterCommandBuffer, Camera> action)
			{
				commands[evt] = action;
			}

			public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt);

			public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
			{
				if (commands.TryGetValue(evt, out var action))
				{
					try
					{
						action?.Invoke(commandBuffer, camera);
					}
					catch (System.Exception ex)
					{
						Debug.LogError($"Preview ground command failed: {ex.Message}");
					}
				}
			}

			private void OnDestroy() => commands.Clear();
		}
	}
}