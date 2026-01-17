using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TMPro;
using MassiveHadronLtd;
using System.Linq;

namespace ClassicTilestorm
{
	public class DefinitionEditorPanel : UIPanel
	{
		[Header("UI References")]
		[SerializeField] private Button closeButton;
		[SerializeField] private ScrollRect definitionScrollView;
		[SerializeField] private Transform contentParent;                    // list of definitions
		[SerializeField] private GameObject definitionListItemPrefab;

		[SerializeField] private Button ButtonInsert;
		[SerializeField] private Button ButtonDelete;
		[SerializeField] private Button ButtonMoveUp;
		[SerializeField] private Button ButtonMoveDown;

		[Header("Preview")]
		[SerializeField] private RawImage previewImage;

		[Header("Dynamic Properties Panel")]
		[SerializeField] private RectTransform propertiesRect;               // ← Grid Layout Group parent
		[SerializeField] private GameObject flagTogglePrefab;                // ← Toggle + Label prefab

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
		[SerializeField] private float autoRotateSpeed = -15f;

		[Header("Ground Plane Settings")]
		[SerializeField] private Color groundColor = Color.white;
		[SerializeField] private float groundSize = 2.5f;
		[SerializeField] private float groundY = -0.02f;
		[SerializeField] private float groundUVScale = 1f;
		[SerializeField] private Texture2D groundOverrideTexture;

		// ── Runtime ────────────────────────────────────────────────────────────
		private readonly List<Toggle> spawnedDefinitionToggles = new();
		private ToggleGroup toggleGroup;
		private string selectedDefinitionId;

		private PreviewSceneController previewCtrl;
		private CommandRenderModelData currentModelData;
		private GimbalOrbitController orbitController;

		private Definition CurrentDefinition =>
			string.IsNullOrEmpty(selectedDefinitionId) ? null :
			ResourceManager.GetDefinition(selectedDefinitionId);

		// Dynamic flag toggles
		private readonly List<(Toggle toggle, FlagInfo flag)> spawnedFlagControls = new();

		private readonly struct FlagInfo
		{
			public readonly string InternalName;
			public readonly string DisplayName;
			public readonly Func<Definition, bool> GetValue;
			public readonly Action<Definition, bool> SetValue;

			public FlagInfo(string internalName, string displayName,
						   Func<Definition, bool> getter,
						   Action<Definition, bool> setter)
			{
				InternalName = internalName;
				DisplayName = displayName;
				GetValue = getter;
				SetValue = setter;
			}
		}

		private static readonly IReadOnlyList<FlagInfo> AllFlags = new List<FlagInfo>
		{
            // ── Connection directions ──────────────────────────────────────────────
            new("North",       "Nav North",   d => d.bNorth,       (d, v) => d.bNorth = v),
			new("East",        "Nav East",    d => d.bEast,        (d, v) => d.bEast = v),
			new("South",       "Nav South",   d => d.bSouth,       (d, v) => d.bSouth = v),
			new("West",        "Nav West",    d => d.bWest,        (d, v) => d.bWest = v),

            // ── other properites ──────────────────────────────────────────────
            new("Drag",        "Can Drag",        d => d.bDrag,        (d, v) => d.bDrag = v),
			new("Roll",        "Can Roll",        d => d.bRoll,        (d, v) => d.bRoll = v),
			new("Dock",        "Can Dock",        d => d.bDock,        (d, v) => d.bDock = v),
			new("Door",        "Is Door",         d => d.bDoor,        (d, v) => d.bDoor = v),
			new("Start",       "Start Point",     d => d.bStart,       (d, v) => d.bStart = v),
			new("End",         "End Point",       d => d.bEnd,         (d, v) => d.bEnd = v),
			new("Console",     "Is Console",      d => d.bConsole,     (d, v) => d.bConsole = v),
			new("PuzzleBlock", "Puzzle Block",    d => d.bPuzzleBlock, (d, v) => d.bPuzzleBlock = v),
			new("Sway",        "Sways",           d => d.bSway,        (d, v) => d.bSway = v),
			new("Wash",        "Bouyant",         d => d.bWash,        (d, v) => d.bWash = v),
            // ← Add new flags here when you create them in Definition.cs
        };

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
			)
			{
				AutoRotateSpeed = autoRotateSpeed,
				AutoRotateTimeout = 3f,
				EnableInertia = true
			};

			if (ButtonInsert) ButtonInsert.onClick.AddListener(InsertDefinition);
			if (ButtonDelete) ButtonDelete.onClick.AddListener(DeleteDefinition);
			if (ButtonMoveUp) ButtonMoveUp.onClick.AddListener(MoveDefinitionUp);
			if (ButtonMoveDown) ButtonMoveDown.onClick.AddListener(MoveDefinitionDown);

			// Create dynamic flag toggles once
			CreateFlagToggles();

			// Setup preview
			InitializePreview();
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			RefreshDefinitionList();
		}

		protected override void OnDisable()
		{
			CleanupPreview();
			ClearDefinitionListItems();
			base.OnDisable();
		}

		private void Update()
		{
			if (previewCtrl == null) return;
			orbitController.Update();
			previewCtrl.UpdateAndRender();
		}

		private Toggle pendingScrollTarget;
		private void LateUpdate()
		{
			if (pendingScrollTarget == null)
				return;

			Canvas.ForceUpdateCanvases();
			ScrollToToggle(pendingScrollTarget);
			pendingScrollTarget = null;
		}

		private int GetSelectedIndex()
		{
			if (string.IsNullOrEmpty(selectedDefinitionId))
				return -1;

			for (int i = 0; i < ResourceManager.Definitions.Count; i++)
				if (ResourceManager.Definitions[i].id == selectedDefinitionId)
					return i;

			return -1;
		}

		private void InsertDefinition()
		{
			if (ResourceManager.database == null) return;

			int n = 1;
			string newId;
			do
			{
				newId = $"new_tile_id({n:000})";
				n++;
			}
			while (ResourceManager.Definitions.Any(d => d.id == newId));

			var def = new Definition
			{
				id = newId,
				model = "tile_flat",
				texture = "Default"
			};

			ResourceManager.InsertDefinitionAfter(selectedDefinitionId, def);

			selectedDefinitionId = def.id;
			RefreshDefinitionList();
		}

		private void DeleteDefinition()
		{
			if (ResourceManager.database == null) return;

			ResourceManager.DeleteDefinition(selectedDefinitionId);

			var defs = ResourceManager.Definitions.ToList();
			if (defs.Count == 0)
			{
				selectedDefinitionId = null;
			}
			else
			{
				int index = GetSelectedIndex();
				int newIndex = Mathf.Clamp(index, 0, defs.Count - 1);
				selectedDefinitionId = defs[newIndex].id;
			}

			RefreshDefinitionList();
		}

		private void MoveDefinitionUp()
		{
			if (ResourceManager.database == null) return;

			ResourceManager.MoveDefinitionUp(selectedDefinitionId);
			RefreshDefinitionList();
		}

		private void MoveDefinitionDown()
		{
			if (ResourceManager.database == null) return;

			ResourceManager.MoveDefinitionDown(selectedDefinitionId);
			RefreshDefinitionList();
		}

		// ───────────────────── DYNAMIC FLAG TOGGLES ─────────────────────────────

		private void CreateFlagToggles()
		{
			// Clear existing children
			for (int i = propertiesRect.childCount - 1; i >= 0; i--)
			{
				Destroy(propertiesRect.GetChild(i).gameObject);
			}
			spawnedFlagControls.Clear();

			foreach (var flag in AllFlags)
			{
				var instance = Instantiate(flagTogglePrefab, propertiesRect);
				var toggle = instance.GetComponent<Toggle>();
				var label = instance.GetComponentInChildren<TMP_Text>();

				if (toggle == null || label == null)
				{
					Debug.LogError("Flag toggle prefab missing Toggle or TMP_Text component!");
					Destroy(instance);
					continue;
				}

				label.text = flag.DisplayName;
				toggle.isOn = false;

				spawnedFlagControls.Add((toggle, flag));

				// Value changed listener
				toggle.onValueChanged.AddListener(isOn =>
				{
					var def = CurrentDefinition;
					if (def == null) return;
					flag.SetValue(def, isOn);
					// Optional: ResourceManager.MarkDefinitionDirty(selectedDefinitionId);
				});
			}
		}

		private void SyncFlagToggles()
		{
			var def = CurrentDefinition;
			bool hasDef = def != null;

			foreach (var (toggle, flag) in spawnedFlagControls)
			{
				bool value = hasDef && flag.GetValue(def);
				toggle.SetIsOnWithoutNotify(value);
				toggle.interactable = hasDef;
			}
		}

		// ───────────────────── DEFINITION LIST ──────────────────────────────────

		private void RefreshDefinitionList()
		{
			ClearDefinitionListItems();

			if (ResourceManager.Definitions.Count == 0)
			{
				selectedDefinitionId = null;
				return;
			}

			// Create new list items
			foreach (var def in ResourceManager.Definitions)
			{
				CreateDefinitionListItem(def);
			}

			// Try to get navigator and rebuild properly
			if (definitionScrollView.TryGetComponent<ScrollViewKeyboardNavigator>(out var navigator))
			{
				// Give Unity a moment to initialize new components
				StartCoroutine(FinishRebuild(navigator));
			}

			// Try restore previous selection or select first item
			string targetId = selectedDefinitionId;
			if (string.IsNullOrEmpty(targetId) && ResourceManager.Definitions.Count > 0)
				targetId = ResourceManager.Definitions[0].id;

			SetToggleById(targetId);
		}

		private System.Collections.IEnumerator FinishRebuild(ScrollViewKeyboardNavigator navigator)
		{
			yield return null;  // let Start/OnEnable run
			yield return null;  // extra safety frame

			navigator.ClearAndRebuild();
		}

		private void CreateDefinitionListItem(Definition def)
		{
			var go = Instantiate(definitionListItemPrefab, contentParent);
			var toggle = go.GetComponent<Toggle>();

			if (toggle == null)
			{
				Destroy(go);
				return;
			}

			toggle.group = toggleGroup;
			spawnedDefinitionToggles.Add(toggle);

			toggle.onValueChanged.AddListener(isOn =>
			{
				if (isOn) SelectDefinition(def.id);
			});

			var label = go.GetComponentInChildren<TMP_Text>();
			if (label != null)
				label.text = $"{def.id} ({def.model ?? "—"})";

			// Add the handler that talks to the navigator
			go.AddComponent<ScrollViewKeyboardNavigator.ItemSelectionHandler>();
		}

		private void ClearDefinitionListItems()
		{
			foreach (var t in spawnedDefinitionToggles)
				if (t != null) Destroy(t.gameObject);

			spawnedDefinitionToggles.Clear();
		}

		private void SetToggleById(string defId)
		{
			foreach (var t in spawnedDefinitionToggles)
			{
				var label = t.GetComponentInChildren<TMP_Text>();
				if (label != null && label.text.StartsWith(defId))
				{
					t.isOn = true;
					pendingScrollTarget = t;
					return;
				}
			}
		}

		private void SelectDefinition(string defId)
		{
			selectedDefinitionId = defId;
			UpdatePreview(defId);
			SyncAllProperties();
		}

		private void SyncAllProperties()
		{
			SyncFlagToggles();
			// Add other property syncs here in future if needed
		}

		// ───────────────────── SCROLL HELPERS ───────────────────────────────────

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

		// ───────────────────── PREVIEW ──────────────────────────────────────────

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
	}
}