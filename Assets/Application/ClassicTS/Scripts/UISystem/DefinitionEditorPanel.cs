using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
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
		[SerializeField] private Vector2 previewResolution = new Vector2(320, 240);

		[Header("Preview Settings - Camera")]
		[SerializeField] private float defaultFOV = 60f;
		[SerializeField] private float sizeToDistanceFactor = 1f;
		[SerializeField] private float defaultTiltAngle = 30f;
		[SerializeField] private float minTiltAngle = 0f;
		[SerializeField] private float maxTiltAngle = 90f;
		[SerializeField] private float minDistance = 0.8f;
		[SerializeField] private float maxDistance = 10f;

		[SerializeField] private float dragOrbitSensitivity = 0.2f;
		[SerializeField] private float dragTiltSensitivity = 0.2f;
		[SerializeField] private float scrollZoomSensitivity = 0.5f;

		[SerializeField] private float autoRotateSpeed = 15f;

		[Header("Preview Ground Plane")]
		[SerializeField] private Color groundColor = new Color(1f, 1f, 1f);
		[SerializeField] private float groundSize = 2.5f;
		[SerializeField] private float groundY = -0.02f;
		[SerializeField] private float groundUVScale = 1f;

		[Header("Ground Texture Override")]
		[SerializeField] private Texture2D groundOverrideTexture;

		// ── Runtime ───────────────────────────────────────────────────────
		private RenderTexture previewRenderTexture;
		private CommandRenderScene commandScene;
		private CommandRenderCamera commandCamera;

		private string selectedDefinitionId;

		private readonly List<GameObject> spawnedListItems = new();

		private Mesh groundMesh;
		private Material groundMat;
		private Texture2D groundTex;
		private CommandRenderModelData currentModelData;
		private CommandRenderModelData groundModelData;

		// Camera control
		private Vector3 gimbalPosition;
		private float currentOrbitAngle = 0f;
		private float currentTiltAngle;
		private float currentDistance;

		private float lastInputTime = -999f;
		private const float AutoRotateDelay = 3f;

		// RT resize tracking
		private RectTransform previewRect;
		private Vector2 lastPreviewSize;

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
			if (commandCamera != null && previewRenderTexture != null && previewRenderTexture.IsCreated())
			{
				HandleRenderTextureResize();
				commandCamera.Render();
			}

			if (autoRotateSpeed > 0.01f && Time.unscaledTime - lastInputTime > AutoRotateDelay)
			{
				currentOrbitAngle -= autoRotateSpeed * Time.deltaTime;
				UpdateCameraTransform();
			}
		}

		protected override void OnDestroy()
		{
			CleanupPreview();
			base.OnDestroy();
		}

		// ─────────────────────────── LIST ────────────────────────────────

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
				if (item != null) Destroy(item);
			spawnedListItems.Clear();
		}

		private void CreateDefinitionListItem(Definition def)
		{
			if (definitionListItemPrefab == null) return;

			var go = Instantiate(definitionListItemPrefab, contentParent);
			spawnedListItems.Add(go);

			var item = go.GetComponent<DefinitionListItem>();
			if (item == null) return;

			item.Initialize(def.id, SelectDefinition);

			if (item.label != null)
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
			lastInputTime = -999f; // Reset autorotate timer

			foreach (var go in spawnedListItems)
			{
				if (go == null) continue;
				var item = go.GetComponent<DefinitionListItem>();
				if (item != null) item.SetSelected(item.DefinitionId == selectedDefinitionId);
			}

			UpdatePreview(defId);
		}

		// ──────────────────────── PREVIEW SETUP ──────────────────────────

		private void CreatePreviewSetup()
		{
			if (previewImage == null) return;

			previewRect = previewImage.GetComponent<RectTransform>();

			ColorUtility.TryParseHtmlString("#21B2E1", out Color hashColor);

			// Create camera directly (no parent at first, or set it)
			commandCamera = new CommandRenderCamera("PreviewCamera", previewRenderTexture, hashColor, defaultFOV);//null,// find a suitable parent object, for now null / in sceneroot
			commandScene = new CommandRenderScene();
			commandCamera.AssignCommandProvider(commandScene);

			CreateGroundPlane();

			if (!previewImage.gameObject.TryGetComponent<PreviewCameraInput>(out _))
				previewImage.gameObject.AddComponent<PreviewCameraInput>(); 
			
			lastPreviewSize = Vector2.zero;
			HandleRenderTextureResize();
		}

		private void HandleRenderTextureResize()
		{
			if (!previewRect) return;

			Vector2 size = previewRect.rect.size;
			if (size.x == 0 || size.y == 0) size = previewResolution;//default

			if (size == lastPreviewSize || size.x < 16 || size.y < 16) return;

			lastPreviewSize = size;

			int w = Mathf.RoundToInt(size.x);
			int h = Mathf.RoundToInt(size.y);

			if (previewRenderTexture && previewRenderTexture.width == w && previewRenderTexture.height == h)
				return;

			if (previewRenderTexture)
				previewRenderTexture.Release();

			previewRenderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
			previewRenderTexture.Create();

			commandCamera.targetTexture = previewRenderTexture;
			commandCamera.aspect = (float)w / h;

			previewImage.texture = previewRenderTexture;
		}

		private void CreateGroundPlane()
		{
			groundMesh = MeshUtils.GenerateQuadXZ(groundSize, groundUVScale, "PreviewGroundMesh");

			groundTex = groundOverrideTexture != null ? groundOverrideTexture : TextureUtils.GenerateXorTexture256();

			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			groundMat = new Material(shader)
			{
				name = "PreviewGroundMat",
				hideFlags = HideFlags.HideAndDontSave
			};

			groundMat.SetFloat("_Surface", 0f);
			groundMat.SetTexture("_BaseMap", groundTex);
			groundMat.SetColor("_BaseColor", groundColor);

			groundModelData = new CommandRenderModelData(
				groundMesh,
				new Material[] { groundMat },
				Matrix4x4.Translate(Vector3.up * groundY));
		}

		private void UpdatePreview(string defId)
		{
			currentModelData = null;

			var def = ResourceManager.GetDefinition(defId);
			if (def == null || string.IsNullOrEmpty(def.model)) return;

			currentModelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);

			float modelSize = currentModelData?.bounds.size.magnitude ?? 5f;
			currentDistance = modelSize * sizeToDistanceFactor;
			currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

			currentTiltAngle = defaultTiltAngle;
			currentOrbitAngle = 0f;

			UpdateCameraTransform();

			commandScene.SetModels(new[] { groundModelData, currentModelData });
		}

		private void UpdateCameraTransform()
		{
			if (commandCamera == null) return;

			var gimbalY = currentModelData != null ? currentModelData.bounds.max.y * 0.5f : 1f;
			gimbalPosition = Vector3.up * gimbalY;

			Quaternion rotation = Quaternion.Euler(currentTiltAngle, currentOrbitAngle, 0f);
			Vector3 forward = rotation * Vector3.forward;
			Vector3 cameraPosition = gimbalPosition - forward * currentDistance;

			commandCamera.position = cameraPosition;
			commandCamera.rotation = rotation;
		}

		// ───────────────────── CAMERA INPUT ──────────────────────────────

		public void DragPreviewCamera(Vector2 delta)
		{
			lastInputTime = Time.unscaledTime;

			currentOrbitAngle += delta.x * dragOrbitSensitivity;
			currentTiltAngle -= delta.y * dragTiltSensitivity;
			currentTiltAngle = Mathf.Clamp(currentTiltAngle, minTiltAngle, maxTiltAngle);

			UpdateCameraTransform();
		}

		public void ZoomPreviewCamera(float scroll)
		{
			lastInputTime = Time.unscaledTime;

			currentDistance -= scroll * scrollZoomSensitivity;
			currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

			UpdateCameraTransform();
		}

		private void CleanupPreview()
		{
			if (previewRenderTexture != null)
			{
				previewRenderTexture.Release();
				previewRenderTexture = null;
			}

			if (groundMesh) DestroyImmediate(groundMesh);
			if (groundMat) DestroyImmediate(groundMat);
			if (groundTex != null && groundTex != groundOverrideTexture)
				DestroyImmediate(groundTex);

			commandCamera?.Destroy();
			commandCamera = null;
			commandScene?.Destroy();
			commandScene = null;

			if (previewImage) previewImage.texture = null;
		}

		// ───────────────────── CAMERA INPUT HELPER ───────────────────────

		private class PreviewCameraInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
		{
			private DefinitionEditorPanel panel;
			private Vector2 lastPos;

			private void Awake()
			{
				panel = GetComponentInParent<DefinitionEditorPanel>();
			}

			public void OnPointerDown(PointerEventData e)
			{
				lastPos = e.position;
				if (panel != null) panel.lastInputTime = Time.unscaledTime;
			}

			public void OnDrag(PointerEventData e)
			{
				Vector2 delta = e.position - lastPos;
				lastPos = e.position;
				panel?.DragPreviewCamera(delta);
			}

			public void OnScroll(PointerEventData e)
			{
				panel?.ZoomPreviewCamera(e.scrollDelta.y);
			}
		}
	}
}