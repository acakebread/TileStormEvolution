using UnityEngine;
using UnityEngine.UI;
using System;
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
		[SerializeField] private Toggle toggleDoor;
		[SerializeField] private Toggle toggleStart;
		[SerializeField] private Toggle toggleEnd;
		[SerializeField] private Toggle toggleConsole;
		[SerializeField] private Toggle togglePuzzleBlock;
		[SerializeField] private Toggle toggleSway;
		[SerializeField] private Toggle toggleWash;

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

		// Flag bindings (clean & maintainable)
		private readonly List<FlagBinding> flagBindings = new();

		private readonly struct FlagBinding
		{
			public readonly Toggle Toggle;
			public readonly Action<bool> SetValue;
			public readonly Func<bool> GetValue;

			public FlagBinding(Toggle toggle, Action<bool> setter, Func<bool> getter)
			{
				Toggle = toggle;
				SetValue = setter;
				GetValue = getter;
			}
		}

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

			SetupFlagBindings();
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

		// ───────────────────── FLAG BINDINGS ─────────────────────

		private void SetupFlagBindings()
		{
			flagBindings.Clear();

			AddBinding(toggleDrag, v => CurrentDefinition.bDrag = v, () => CurrentDefinition?.bDrag ?? false);
			AddBinding(toggleRoll, v => CurrentDefinition.bRoll = v, () => CurrentDefinition?.bRoll ?? false);
			AddBinding(toggleDock, v => CurrentDefinition.bDock = v, () => CurrentDefinition?.bDock ?? false);
			AddBinding(toggleDoor, v => CurrentDefinition.bDoor = v, () => CurrentDefinition?.bDoor ?? false);
			AddBinding(toggleStart, v => CurrentDefinition.bStart = v, () => CurrentDefinition?.bStart ?? false);
			AddBinding(toggleEnd, v => CurrentDefinition.bEnd = v, () => CurrentDefinition?.bEnd ?? false);
			AddBinding(toggleConsole, v => CurrentDefinition.bConsole = v, () => CurrentDefinition?.bConsole ?? false);
			AddBinding(togglePuzzleBlock, v => CurrentDefinition.bPuzzleBlock = v, () => CurrentDefinition?.bPuzzleBlock ?? false);
			AddBinding(toggleSway, v => CurrentDefinition.bSway = v, () => CurrentDefinition?.bSway ?? false);
			AddBinding(toggleWash, v => CurrentDefinition.bWash = v, () => CurrentDefinition?.bWash ?? false);

			// Hook up all toggle events once
			foreach (var binding in flagBindings)
			{
				if (binding.Toggle == null) continue;

				binding.Toggle.onValueChanged.AddListener(value =>
				{
					if (CurrentDefinition == null) return;
					binding.SetValue(value);
					// Optional: Mark dirty / needs save here
					// ResourceManager.MarkDefinitionDirty(selectedDefinitionId);
				});
			}
		}

		private void AddBinding(Toggle toggle, Action<bool> setter, Func<bool> getter)
		{
			if (toggle != null)
				flagBindings.Add(new FlagBinding(toggle, setter, getter));
		}

		// ───────────────────── LIST MANAGEMENT ─────────────────────

		private void RefreshDefinitionList()
		{
			ClearListItems();
			if (ResourceManager.Definitions.Count == 0) return;

			foreach (var def in ResourceManager.Definitions)
				CreateDefinitionListItem(def);

			if (definitionScrollView.TryGetComponent<ScrollViewKeyboardNavigator>(out var navigator))
				navigator.ForceRefresh();

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

			toggle.onValueChanged.AddListener(isOn =>
			{
				if (isOn) SelectDefinition(def.id);
			});

			var label = go.GetComponentInChildren<TMPro.TMP_Text>();
			if (label)
				label.text = $"{def.id} ({def.model ?? "—"})";

			if (definitionScrollView.TryGetComponent<ScrollViewKeyboardNavigator>(out _))
				go.AddComponent(typeof(ScrollViewKeyboardNavigator.ItemSelectionHandler));
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

		// ───────────── FLAG SYNC ─────────────

		private void SyncAllFlags()
		{
			var def = CurrentDefinition;
			bool hasDefinition = def != null;

			foreach (var binding in flagBindings)
			{
				if (binding.Toggle == null) continue;

				bool value = hasDefinition ? binding.GetValue() : false;

				binding.Toggle.SetIsOnWithoutNotify(value);
				binding.Toggle.interactable = hasDefinition;
			}
		}
	}
}