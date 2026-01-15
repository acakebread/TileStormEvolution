using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class ModelEditorPanel : UIPanel
	{
		[Header("UI References")]
		[SerializeField] private Button closeButton;
		[SerializeField] private ScrollRect definitionScrollView;
		[SerializeField] private Transform contentParent;
		[SerializeField] private GameObject definitionListItemPrefab;
		[SerializeField] private RawImage previewImage;

		[Header("Preview Settings")]
		[SerializeField] private Color backgroundColor = new Color(0.129f, 0.698f, 0.882f); // #21B2E1
		[SerializeField] private float fieldOfView = 60f;
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

		[Header("Ground Plane Settings")]
		[SerializeField] private Color groundColor = Color.white;
		[SerializeField] private float groundSize = 2.5f;
		[SerializeField] private float groundY = -0.02f;
		[SerializeField] private float groundUVScale = 1f;
		[SerializeField] private Texture2D groundOverrideTexture; // optional

		// ── Runtime ────────────────────────────────────────────────
		private readonly List<Toggle> spawnedToggles = new();
		private ToggleGroup toggleGroup;
		private string selectedDefinitionId;

		private PreviewSceneController previewCtrl;
		private CommandRenderModelData currentModelData;

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

			toggleGroup = contentParent.GetComponent<ToggleGroup>() ?? contentParent.gameObject.AddComponent<ToggleGroup>();
			toggleGroup.allowSwitchOff = false;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			InitializePreview();
			RefreshDefinitionList();
		}

		protected override void OnDisable()
		{
			CleanupPreview();
			ClearListItems();
			base.OnDisable();
		}

		private void Update()
		{
			if (previewCtrl == null) return;

			previewCtrl.UpdateRenderTextureSizeIfNeeded();

			// Auto-rotate
			if (autoRotateSpeed > 0.01f && Time.unscaledTime - lastInputTime > AutoRotateDelay)
			{
				currentOrbitAngle -= autoRotateSpeed * Time.deltaTime;
				UpdateCameraTransform();
			}

			// Very important: we must call Render explicitly
			previewCtrl.Camera?.Render();
		}

		// ───────────────────── LIST MANAGEMENT ─────────────────────

		private void RefreshDefinitionList()
		{
			ClearListItems();
			if (ResourceManager.Definitions.Count == 0) return;

			foreach (var def in ResourceManager.Definitions)
				CreateDefinitionListItem(def);

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
				Debug.LogError("Definition list item prefab must have a Toggle component!");
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
			lastInputTime = -999f; // reset auto-rotate timer
			UpdatePreview(defId);
		}

		// ───────────── PREVIEW INITIALIZATION ─────────────

		private void InitializePreview()
		{
			if (previewImage == null) return;

			previewCtrl = new PreviewSceneController(previewImage, previewImage.GetComponent<RectTransform>())
			{
				BackgroundColor = backgroundColor,
				FieldOfView = fieldOfView,
				GroundColor = groundColor,
				GroundSize = groundSize,
				GroundY = groundY,
				GroundUVScale = groundUVScale,
				GroundOverrideTexture = groundOverrideTexture,
				// DefaultResolution = ... if you want to override it too
			};

			// Setup input callbacks
			SetupPreviewInput();
		}

		private void SetupPreviewInput()
		{
			if (!previewImage.TryGetComponent<PointerDragScrollHandler>(out var handler))
			{
				handler = previewImage.gameObject.AddComponent<PointerDragScrollHandler>();
			}

			handler.Setup(
				onDown: () => lastInputTime = Time.unscaledTime,
				onDrag: DragPreviewCamera,
				onScroll: ZoomPreviewCamera
				//onUp:    () => { /* optional */ }
			);
		}

		// ───────────── PREVIEW UPDATE & CONTROL ─────────────

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

			previewCtrl.SetModel(currentModelData);
			UpdateCameraTransform();
		}

		private void UpdateCameraTransform()
		{
			if (previewCtrl?.Camera == null) return;

			var gimbalY = currentModelData?.bounds.max.y * 0.5f ?? 1f;
			var gimbalPosition = Vector3.up * gimbalY;

			var rotation = Quaternion.Euler(currentTiltAngle, currentOrbitAngle, 0f);
			var forward = rotation * Vector3.forward;
			var cameraPosition = gimbalPosition - forward * currentDistance;

			previewCtrl.Camera.position = cameraPosition;
			previewCtrl.Camera.rotation = rotation;
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
			if (previewCtrl != null)
			{
				previewCtrl.Dispose();
				previewCtrl = null;
			}

			currentModelData = null;
		}
	}
}