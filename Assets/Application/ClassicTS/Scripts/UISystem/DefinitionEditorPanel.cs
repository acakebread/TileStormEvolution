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
		[SerializeField] private Color groundColor = Color.white;
		[SerializeField] private float groundSize = 2.5f;
		[SerializeField] private float groundY = -0.02f;
		[SerializeField] private float groundUVScale = 1f;

		[Header("Ground Texture Override")]
		[SerializeField] private Texture2D groundOverrideTexture;

		// ── Runtime ────────────────────────────────────────────────
		private readonly List<Toggle> spawnedToggles = new();
		private ToggleGroup toggleGroup;
		private string selectedDefinitionId;

		private CommandRenderModelData currentModelData;
		private CommandRenderModelData groundModelData;
		private Mesh groundMesh;
		private Material groundMat;
		private Texture2D groundTex;

		private RectTransform previewRect;
		private RenderTexture previewRenderTexture;
		private Vector2 lastPreviewSize;

		private CommandRenderScene commandScene;
		private CommandRenderCamera commandCamera;

		private Vector3 gimbalPosition;
		private float currentOrbitAngle;
		private float currentTiltAngle;
		private float currentDistance;

		private float lastInputTime = -999f;
		private const float AutoRotateDelay = 3f;

		// ────────────────────────────────────────────────

		protected override void Awake()
		{
			base.Awake();

			if (closeButton)
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));

			if (!contentParent && definitionScrollView)
				contentParent = definitionScrollView.content;

			// Add or find the ToggleGroup on the content
			toggleGroup = contentParent.GetComponent<ToggleGroup>();
			if (!toggleGroup)
				toggleGroup = contentParent.gameObject.AddComponent<ToggleGroup>();

			toggleGroup.allowSwitchOff = false;
		}

		public override void OnPanelOpened()
		{
			base.OnPanelOpened();
			CleanupPreview();
			CreatePreviewSetup();
			RefreshDefinitionList();
		}

		public override void OnPanelClosed()
		{
			base.OnPanelClosed();
			CleanupPreview();
			ClearListItems();
		}

		private void Update()
		{
			if (commandCamera != null && previewRenderTexture && previewRenderTexture.IsCreated())
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

		// ───────────────────── LIST ─────────────────────

		private void RefreshDefinitionList()
		{
			ClearListItems();
			if (ResourceManager.Definitions.Count == 0) return;

			foreach (var def in ResourceManager.Definitions)
				CreateDefinitionListItem(def);

			// Select first item if none selected
			string id = selectedDefinitionId ?? ResourceManager.Definitions[0].id;
			SetToggleById(id);
		}

		private void ClearListItems()
		{
			foreach (var t in spawnedToggles)
				if (t) Destroy(t.gameObject);

			spawnedToggles.Clear();
		}

		private void CreateDefinitionListItem(Definition def)
		{
			var go = Instantiate(definitionListItemPrefab, contentParent);
			var toggle = go.GetComponent<Toggle>();

			if (!toggle)
			{
				Debug.LogError("Prefab must have a Toggle component!");
				return;
			}

			toggle.group = toggleGroup;
			spawnedToggles.Add(toggle);

			var label = go.GetComponentInChildren<TMPro.TMP_Text>();
			if (label)
				label.text = $"{def.id} ({def.model ?? "—"})";

			toggle.onValueChanged.AddListener(isOn =>
			{
				if (isOn)
					SelectDefinition(def.id);
			});
		}

		private void SetToggleById(string defId)
		{
			foreach (var t in spawnedToggles)
			{
				var label = t.GetComponentInChildren<TMPro.TMP_Text>();
				if (label != null && label.text.StartsWith(defId))
				{
					t.isOn = true;
					return;
				}
			}
		}

		private void SelectDefinition(string defId)
		{
			selectedDefinitionId = defId;
			lastInputTime = -999f;
			UpdatePreview(defId);
		}

		// ───────────── PREVIEW / CAMERA CODE ─────────────

		private void CreatePreviewSetup()
		{
			if (previewImage == null) return;

			previewRect = previewImage.GetComponent<RectTransform>();

			ColorUtility.TryParseHtmlString("#21B2E1", out Color hashColor);

			commandCamera = new CommandRenderCamera("PreviewCamera", previewRenderTexture, hashColor, defaultFOV);
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
			if (size.x == 0 || size.y == 0) size = previewResolution;

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
			groundModelData = new CommandRenderModelData(groundMesh, new Material[] { groundMat }, Matrix4x4.Translate(Vector3.up * groundY));
		}

		private void UpdatePreview(string defId)
		{
			currentModelData = null;

			var def = ResourceManager.GetDefinition(defId);
			if (def == null || string.IsNullOrEmpty(def.model)) return;

			currentModelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);

			float modelSize = currentModelData?.bounds.size.magnitude ?? 5f;
			currentDistance = Mathf.Clamp(modelSize * sizeToDistanceFactor, minDistance, maxDistance);

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
