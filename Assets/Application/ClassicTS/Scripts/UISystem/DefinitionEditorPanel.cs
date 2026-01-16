using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
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

		[SerializeField] private Toggle toggleDrag;
		[SerializeField] private Toggle toggleRoll;
		[SerializeField] private Toggle toggleDock;

		[Header("Preview Settings")]
		[SerializeField] private Color backgroundColor = new Color(0.129f, 0.698f, 0.882f);
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
		[SerializeField] private Texture2D groundOverrideTexture;

		// ── Runtime ────────────────────────────────────────────────
		private readonly List<Toggle> spawnedToggles = new();
		private ToggleGroup toggleGroup;
		private string selectedDefinitionId;

		private PreviewSceneController previewCtrl;
		private CommandRenderModelData currentModelData;
		private GimbalOrbitController orbitController;

		private Definition CurrentDefinition =>
			string.IsNullOrEmpty(selectedDefinitionId) ? null :
			ResourceManager.GetDefinition(selectedDefinitionId);

		// ────────────────────────────────────────────────

		protected override void Awake()
		{
			base.Awake();

			if (closeButton)
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));

			if (!contentParent && definitionScrollView)
				contentParent = definitionScrollView.content;

			toggleGroup = contentParent.GetComponent<ToggleGroup>()
				?? contentParent.gameObject.AddComponent<ToggleGroup>();
			toggleGroup.allowSwitchOff = false;

			orbitController = new GimbalOrbitController(
				dragOrbitSens: dragOrbitSensitivity,
				dragTiltSens: dragTiltSensitivity,
				scrollZoomSens: scrollZoomSensitivity,
				minTilt: minTiltAngle,
				maxTilt: maxTiltAngle,
				minDist: minDistance,
				maxDist: maxDistance,
				sizeToDistFactor: sizeToDistanceFactor,
				defaultTilt: defaultTiltAngle
			);

			orbitController.AutoRotateSpeed = autoRotateSpeed;
			orbitController.AutoRotateTimeout = 3f;
			orbitController.EnableInertia = true;

			// Hook up property toggles
			if (toggleDrag) toggleDrag.onValueChanged.AddListener(v => SetDrag(v, true));
			if (toggleRoll) toggleRoll.onValueChanged.AddListener(v => SetRoll(v, true));
			if (toggleDock) toggleDock.onValueChanged.AddListener(v => SetDock(v, true));
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

			orbitController.Update();
			previewCtrl.UpdateAndRender();
		}

		// ───────────────────── LIST MANAGEMENT ─────────────────────

		private void RefreshDefinitionList()
		{
			ClearListItems();
			if (ResourceManager.Definitions.Count == 0) return;

			foreach (var def in ResourceManager.Definitions)
				CreateDefinitionListItem(def);

			// Notify keyboard navigator that the list has changed
			if (definitionScrollView.TryGetComponent<ScrollViewKeyboardNavigator>(out var navigator))
			{
				navigator.ForceRefresh();
			}

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
				Destroy(go);
				return;
			}

			toggle.group = toggleGroup;
			spawnedToggles.Add(toggle);

			// Your own selection logic
			toggle.onValueChanged.AddListener(isOn =>
			{
				if (isOn)
				{
					SelectDefinition(def.id);
				}
			});

			// Label
			var label = go.GetComponentInChildren<TMPro.TMP_Text>();
			if (label)
				label.text = $"{def.id} ({def.model ?? "—"})";

			// Optional: Add support for keyboard navigator (only if it exists)
			if (definitionScrollView.TryGetComponent<ScrollViewKeyboardNavigator>(out _))
			{
				go.AddComponent(typeof(ScrollViewKeyboardNavigator.ItemSelectionHandler));
			}
		}

		private void SetToggleById(string defId)
		{
			foreach (var t in spawnedToggles)
			{
				var label = t.GetComponentInChildren<TMPro.TMP_Text>();
				if (label != null && label.text.StartsWith(defId))
				{
					t.isOn = true;
					ScrollToToggle(t);
					return;
				}
			}
		}

		private void SelectDefinition(string defId)
		{
			selectedDefinitionId = defId;
			UpdatePreview(defId);
			SyncAllFlags();
		}

		// ───────────── HELPERS ─────────────

		private void ScrollToToggle(Toggle toggle)
		{
			if (definitionScrollView == null || toggle == null) return;

			var rt = toggle.GetComponent<RectTransform>();
			if (rt == null) return;

			Canvas.ForceUpdateCanvases();

			var viewport = definitionScrollView.viewport;
			var content = contentParent as RectTransform;

			Vector2 localPoint = content.InverseTransformPoint(rt.position);
			float targetY = -localPoint.y - (rt.rect.height / 2f);

			float viewportHeight = viewport.rect.height;
			float contentHeight = content.rect.height;

			float normalized = Mathf.Clamp01((targetY - viewportHeight / 2f) / (contentHeight - viewportHeight));

			definitionScrollView.verticalNormalizedPosition = 1f - normalized;
		}

		// ───────────── PREVIEW ─────────────

		private void InitializePreview()
		{
			if (previewImage == null) return;

			previewCtrl = new PreviewSceneController(
				previewImage,
				previewImage.GetComponent<RectTransform>())
			{
				BackgroundColor = backgroundColor,
				FieldOfView = fieldOfView,
				GroundColor = groundColor,
				GroundSize = groundSize,
				GroundY = groundY,
				GroundUVScale = groundUVScale,
				GroundOverrideTexture = groundOverrideTexture
			};

			SetupPreviewInput();
			orbitController.OnTransformChanged += ApplyCameraTransform;
		}

		private void SetupPreviewInput()
		{
			if (!previewImage.TryGetComponent<PointerDragScrollHandler>(out var handler))
				handler = previewImage.gameObject.AddComponent<PointerDragScrollHandler>();

			handler.Setup(
				onDrag: orbitController.ProcessDrag,
				onScroll: orbitController.ProcessScroll,
				onUp: orbitController.EndDrag
			);
		}

		private void UpdatePreview(string defId)
		{
			currentModelData = null;
			previewCtrl.ClearModel();

			var def = ResourceManager.GetDefinition(defId);
			if (def != null && !string.IsNullOrEmpty(def.model))
			{
				currentModelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);
				previewCtrl.SetModel(currentModelData);
				orbitController.ResetView(true, currentModelData.bounds);
			}
			else
			{
				orbitController.ResetView(false);
			}
		}

		private void ApplyCameraTransform()
		{
			if (previewCtrl?.Camera == null) return;
			var (pos, rot) = orbitController.GetCameraTransform();
			previewCtrl.Camera.position = pos;
			previewCtrl.Camera.rotation = rot;
		}

		private void CleanupPreview()
		{
			if (previewCtrl != null)
			{
				previewCtrl.Dispose();
				previewCtrl = null;
			}
			currentModelData = null;
			if (orbitController != null)
				orbitController.OnTransformChanged -= ApplyCameraTransform;
		}

		// ───────────── FLAG SETTERS ─────────────

		private void SetDrag(bool value, bool fromUser)
		{
			var def = CurrentDefinition;
			if (def == null)
			{
				toggleDrag?.SetIsOnWithoutNotify(false);
				if (toggleDrag) toggleDrag.interactable = false;
				return;
			}

			if (def.bDrag != value) def.bDrag = value;

			if (toggleDrag != null)
			{
				toggleDrag.SetIsOnWithoutNotify(def.bDrag);
				toggleDrag.interactable = true;
			}
		}

		private void SetRoll(bool value, bool fromUser)
		{
			var def = CurrentDefinition;
			if (def == null)
			{
				toggleRoll?.SetIsOnWithoutNotify(false);
				if (toggleRoll) toggleRoll.interactable = false;
				return;
			}

			if (def.bRoll != value) def.bRoll = value;

			if (toggleRoll != null)
			{
				toggleRoll.SetIsOnWithoutNotify(def.bRoll);
				toggleRoll.interactable = true;
			}
		}

		private void SetDock(bool value, bool fromUser)
		{
			var def = CurrentDefinition;
			if (def == null)
			{
				toggleDock?.SetIsOnWithoutNotify(false);
				if (toggleDock) toggleDock.interactable = false;
				return;
			}

			if (def.bDock != value) def.bDock = value;

			if (toggleDock != null)
			{
				toggleDock.SetIsOnWithoutNotify(def.bDock);
				toggleDock.interactable = true;
			}
		}

		private void SyncAllFlags()
		{
			var def = CurrentDefinition;
			SetDrag(def?.bDrag ?? false, false);
			SetRoll(def?.bRoll ?? false, false);
			SetDock(def?.bDock ?? false, false);
		}
	}
}