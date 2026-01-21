using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TMPro;
using System.Linq;
using MassiveHadronLtd;
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public class DefinitionEditorPanel : UIPanel
	{
		#region Serialized Fields - UI References

		[Header("UI References")]
		[SerializeField] private Button closeButton;
		[SerializeField] private ScrollRect definitionScrollView;
		[SerializeField] private Transform contentParent;
		[SerializeField] private GameObject definitionListItemPrefab;

		[SerializeField] private Button ButtonInsert;
		[SerializeField] private Button ButtonDelete;
		[SerializeField] private Button ButtonMoveUp;
		[SerializeField] private Button ButtonMoveDown;

		[Header("Preview")]
		[SerializeField] private RawImage previewImage;

		[Header("Dynamic Properties Panel")]
		[SerializeField] private RectTransform flagPropertiesRect;
		[SerializeField] private GameObject flagTogglePrefab;

		[Header("ID Input")]
		[SerializeField] private TMP_InputField IDInput;

		[Header("Dropdowns")]
		[SerializeField] private TMP_Dropdown modelDropdown;
		[SerializeField] private string noneModelOptionText = "— None —";

		[SerializeField] private TMP_Dropdown textureSequenceDropdown;
		[SerializeField] private string noneTextureOptionText = "— None —";

		[SerializeField] private TMP_Dropdown materialDropdown;
		[SerializeField] private string noneMaterialOptionText = "— None —";

		#endregion

		#region Serialized Fields - Preview Settings

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
		[SerializeField] private float groundY = -0.01f;
		[SerializeField] private float groundUVScale = 1f;
		[SerializeField] private Texture2D groundOverrideTexture;

		#endregion

		// Runtime state
		private readonly List<Toggle> spawnedDefinitionToggles = new();
		private ToggleGroup toggleGroup;

		private static int lastSelectedDefinitionIndex = 0;

		private PreviewSceneController previewCtrl;
		private CommandRenderModelData currentModelData;
		private GimbalOrbitController orbitController;

		private Definition CurrentDefinition =>
			lastSelectedDefinitionIndex >= 0 && lastSelectedDefinitionIndex < ResourceManager.Definitions.Count
				? ResourceManager.Definitions[lastSelectedDefinitionIndex]
				: null;

		// Flag system
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
			new("North",       "Nav North",   d => d.bNorth,       (d, v) => d.bNorth = v),
			new("East",        "Nav East",    d => d.bEast,        (d, v) => d.bEast = v),
			new("South",       "Nav South",   d => d.bSouth,       (d, v) => d.bSouth = v),
			new("West",        "Nav West",    d => d.bWest,        (d, v) => d.bWest = v),
			new("Drag",        "Can Drag",    d => d.bDrag,        (d, v) => d.bDrag = v),
			new("Roll",        "Can Roll",    d => d.bRoll,        (d, v) => d.bRoll = v),
			new("Dock",        "Can Dock",    d => d.bDock,        (d, v) => d.bDock = v),
			new("Door",        "Is Door",     d => d.bDoor,        (d, v) => d.bDoor = v),
			new("Start",       "Start Point", d => d.bStart,       (d, v) => d.bStart = v),
			new("End",         "End Point",   d => d.bEnd,         (d, v) => d.bEnd = v),
			new("Console",     "Is Console",  d => d.bConsole,     (d, v) => d.bConsole = v),
			new("PuzzleBlock", "Puzzle Block",d => d.bPuzzleBlock, (d, v) => d.bPuzzleBlock = v),
			new("Sway",        "Sways",       d => d.bSway,        (d, v) => d.bSway = v),
			new("Wash",        "Bouyant",     d => d.bWash,        (d, v) => d.bWash = v),
		};

		protected override void Awake()
		{
			base.Awake();

			InitializeUIReferences();
			InitializePreviewController();
			InitializeButtons();
			InitializeDropdownListeners();
			CreateFlagToggles();
		}

		protected override void OnEnable()
		{
			base.OnEnable();

			EnsurePreviewInitialized();
			RefreshDefinitionList();

			PopulateAndSyncDropdowns();
		}

		protected override void OnDisable()
		{
			CleanupPreview();
			ClearDefinitionListItems();
			base.OnDisable();
		}

		private void InitializeUIReferences()
		{
			if (closeButton) closeButton.onClick.AddListener(() => gameObject.SetActive(false));

			if (!contentParent && definitionScrollView)
				contentParent = definitionScrollView.content;

			toggleGroup = contentParent.GetComponent<ToggleGroup>()
				?? contentParent.gameObject.AddComponent<ToggleGroup>();
			toggleGroup.allowSwitchOff = false;
		}

		private void InitializePreviewController()
		{
			orbitController = new GimbalOrbitController(
				dragOrbitSens: dragOrbitSensitivity,
				dragTiltSens: dragTiltSensitivity,
				scrollZoomSens: scrollZoomSensitivity,
				minTilt: minTiltAngle,
				maxTilt: maxTiltAngle,
				minDist: minDistance,
				maxDist: maxDistance,
				sizeToDistFactor: sizeToDistanceFactor,
				defaultTilt: defaultTiltAngle)
			{
				AutoRotateSpeed = autoRotateSpeed,
				AutoRotateTimeout = 3f,
				EnableInertia = true
			};
		}

		private void InitializeButtons()
		{
			if (ButtonInsert) ButtonInsert.onClick.AddListener(InsertDefinition);
			if (ButtonDelete) ButtonDelete.onClick.AddListener(DeleteDefinition);
			if (ButtonMoveUp) ButtonMoveUp.onClick.AddListener(MoveDefinitionUp);
			if (ButtonMoveDown) ButtonMoveDown.onClick.AddListener(MoveDefinitionDown);
		}

		private void InitializeDropdownListeners()
		{
			if (modelDropdown != null)
				modelDropdown.onValueChanged.AddListener(OnModelDropdownValueChanged);

			if (textureSequenceDropdown != null)
				textureSequenceDropdown.onValueChanged.AddListener(OnTextureDropdownValueChanged);

			if (materialDropdown != null)
				materialDropdown.onValueChanged.AddListener(OnMaterialDropdownValueChanged);

			if (IDInput != null)
				IDInput.onEndEdit.AddListener(OnIDInputEndEdit);
		}

		private void PopulateAndSyncDropdowns()
		{
			PopulateModelDropdown();
			PopulateTextureDropdown();
			PopulateMaterialDropdown();

			SyncModelDropdown();
			SyncTextureDropdown();
			SyncMaterialDropdown();
		}

		// ── Dropdown Helpers ────────────────────────────────────────────────────────────────

		private void PopulateDropdown(TMP_Dropdown dropdown, IEnumerable<string> items, string noneOption)
		{
			if (dropdown == null) return;

			dropdown.ClearOptions();

			var options = new List<string> { noneOption };
			options.AddRange(items);

			dropdown.AddOptions(options);
			dropdown.interactable = true;
		}

		private void SyncDropdown(TMP_Dropdown dropdown, string currentValue, string noneOption)
		{
			if (dropdown == null) return;

			if (string.IsNullOrEmpty(currentValue))
			{
				dropdown.SetValueWithoutNotify(0);
				return;
			}

			int index = dropdown.options.FindIndex(opt =>
				opt.text.Equals(currentValue, StringComparison.OrdinalIgnoreCase));

			dropdown.SetValueWithoutNotify(index >= 0 ? index : 0);
		}

		private void PopulateModelDropdown() =>
			PopulateDropdown(modelDropdown, ProjectAssets.GetModelNames(), noneModelOptionText);

		private void PopulateTextureDropdown() =>
			PopulateDropdown(textureSequenceDropdown,
				ResourceManager.TextureSequences
					.Where(ts => !string.IsNullOrEmpty(ts.id))
					.Select(ts => ts.id)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
				noneTextureOptionText);

		private void PopulateMaterialDropdown() =>
			PopulateDropdown(materialDropdown, ProjectAssets.GetMaterialNames(), noneMaterialOptionText);

		private void SyncModelDropdown() =>
			SyncDropdown(modelDropdown, CurrentDefinition?.model, noneModelOptionText);

		private void SyncTextureDropdown() =>
			SyncDropdown(textureSequenceDropdown, CurrentDefinition?.texture, noneTextureOptionText);

		private void SyncMaterialDropdown() =>
			SyncDropdown(materialDropdown, CurrentDefinition?.material, noneMaterialOptionText);

		// ── Event Handlers ──────────────────────────────────────────────────────────────────

		private void OnIDInputEndEdit(string input)
		{
			var def = CurrentDefinition;
			if (def == null) return;

			string newId = (input ?? "").Trim();

			if (string.IsNullOrWhiteSpace(newId) || newId == def.id)
			{
				IDInput.text = def.id;
				return;
			}

			if (!string.IsNullOrEmpty(def.hashid))
			{
				def.id = IDInput.text;
				RefreshDefinitionList();
				return;//safe to exit because we have moved over to the new hashid system - fall through only for legacy support
			}

			// Early duplicate check for nice UI feedback
			if (ResourceManager.Definitions.Any(d => d != def && string.Equals(d.id, newId, StringComparison.Ordinal)))
			{
				Debug.LogWarning($"ID '{newId}' already exists!");
				IDInput.text = def.id;
				return;
			}

			int cellsUpdated = ResourceManager.RenameDefinitionId(def.id, newId);

			if (cellsUpdated >= 0)
			{
				Debug.Log($"Renamed '{def.id}' → '{newId}'  (updated {cellsUpdated} placement{(cellsUpdated == 1 ? "" : "s")})");
				RefreshDefinitionList();  // updates list + usage counts
			}
			else
			{
				// Shouldn't happen due to pre-check, but safety
				IDInput.text = def.id;
			}
		}

		private void OnModelDropdownValueChanged(int index)
		{
			var def = CurrentDefinition;
			if (def == null) return;

			string selected = index >= 0 && index < modelDropdown.options.Count
				? modelDropdown.options[index].text
				: null;

			string newModel = (selected == noneModelOptionText) ? null : selected;

			if (newModel != def.model)
			{
				def.model = newModel;
				def.texture = null;
				def.material = null;
				UpdatePreview(lastSelectedDefinitionIndex);
				SyncTextureDropdown();
				SyncMaterialDropdown();
			}
		}

		private void OnTextureDropdownValueChanged(int index)
		{
			var def = CurrentDefinition;
			if (def == null) return;

			string selected = index >= 0 && index < textureSequenceDropdown.options.Count
				? textureSequenceDropdown.options[index].text
				: null;

			string newTexture = (selected == noneTextureOptionText) ? null : selected;

			if (newTexture != def.texture)
			{
				def.texture = newTexture;
				UpdatePreview(lastSelectedDefinitionIndex);
			}
		}

		private void OnMaterialDropdownValueChanged(int index)
		{
			var def = CurrentDefinition;
			if (def == null) return;

			string selected = index >= 0 && index < materialDropdown.options.Count
				? materialDropdown.options[index].text
				: null;

			string newMaterial = (selected == noneMaterialOptionText) ? null : selected;

			if (newMaterial != def.material)
			{
				def.material = newMaterial;
				UpdatePreview(lastSelectedDefinitionIndex);
			}
		}

		// ── Definition List Management ──────────────────────────────────────────────────────

		private void RefreshDefinitionList()
		{
			ClearDefinitionListItems();

			var defs = ResourceManager.Definitions;
			if (defs.Count == 0)
			{
				lastSelectedDefinitionIndex = -1;
				SyncAllProperties();
				return;
			}

			for (int i = 0; i < defs.Count; i++)
				CreateDefinitionListItem(defs[i], i);

			SetSelectedIndex(lastSelectedDefinitionIndex);
			UpdateDeleteButtonState();
		}

		private void CreateDefinitionListItem(Definition def, int index)
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
				if (isOn) SetSelectedIndex(index);
			});

			var label = go.GetComponentInChildren<TMP_Text>();
			if (label != null)
			{
				string usage = ResourceManager.DefinitionUsageCount(def.id).ToString();
				string hashPart = "";

				if (!string.IsNullOrEmpty(def.hashid))
				{
					hashPart = $" (hash:{def.hashid})";
				}
				// Optional more verbose version during early testing:
				else if (!string.IsNullOrEmpty(def.id))
				{
					string computed = def.GetStableId();
					hashPart = $" (hash:pending → {computed})";
				}

				label.text = $"{def.id} [{usage}]{hashPart}";
			}
		}

		private void ClearDefinitionListItems()
		{
			foreach (var t in spawnedDefinitionToggles)
				if (t != null) Destroy(t.gameObject);

			spawnedDefinitionToggles.Clear();
		}

		private void SetSelectedIndex(int index)
		{
			index = Mathf.Clamp(index, -1, ResourceManager.Definitions.Count - 1);
			lastSelectedDefinitionIndex = index;

			var def = CurrentDefinition;

			IDInput.text = def?.id ?? "";

			SyncAllProperties();
			UpdatePreview(index);
			SyncModelDropdown();
			SyncTextureDropdown();
			SyncMaterialDropdown();
			UpdateDeleteButtonState();

			if (index >= 0 && index < spawnedDefinitionToggles.Count)
				spawnedDefinitionToggles[index].SetIsOnWithoutNotify(true);
		}

		private void SyncAllProperties()
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

		private void UpdateDeleteButtonState()
		{
			if (ButtonDelete == null) return;

			bool hasSelection = lastSelectedDefinitionIndex >= 0;
			bool isUsed = hasSelection && ResourceManager.IsDefinitionUsed(CurrentDefinition?.id);

			ButtonDelete.interactable = hasSelection && !isUsed;

			if (ButtonDelete.GetComponentInChildren<TMP_Text>() is TMP_Text btnText)
				btnText.text = isUsed ? "Delete (used)" : "Delete";
		}

		// ── Preview Management ──────────────────────────────────────────────────────────────

		private void EnsurePreviewInitialized()
		{
			if (previewCtrl != null) return;

			if (previewImage == null)
			{
				Debug.LogError("[DefinitionEditorPanel] previewImage is not assigned!", this);
				return;
			}

			previewCtrl = new PreviewSceneController(previewImage, previewImage.GetComponent<RectTransform>())
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

			if (previewCtrl == null)
				Debug.LogError("[DefinitionEditorPanel] Failed to initialize PreviewSceneController!", this);
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

		private void UpdatePreview(int index)
		{
			if (previewCtrl == null)
			{
				EnsurePreviewInitialized();
				if (previewCtrl == null) return;
			}

			currentModelData = null;
			previewCtrl.ClearModel();

			var def = (index >= 0 && index < ResourceManager.Definitions.Count)
				? ResourceManager.Definitions[index]
				: null;

			if (def != null && !string.IsNullOrEmpty(def.model))
			{
				currentModelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);
				if (currentModelData != null)
				{
					previewCtrl.SetModel(currentModelData);

					// Keep model at authored position (0,0,0)
					// Move ground instead, using existing groundY as clearance/sink
					float lowestY = currentModelData.bounds.min.y;
					previewCtrl.GroundY = lowestY + groundY;   // e.g. if groundY = -0.02f → ground 2 cm below lowest point

					orbitController.ResetView(true, currentModelData.bounds);
				}
				else
					orbitController.ResetView(false);
			}
			else
			{
				// When no model, fall back to serialized groundY (your original default)
				previewCtrl.GroundY = groundY;
				orbitController.ResetView(false);
			}
		}

		private void ApplyCameraTransform()
		{
			if (previewCtrl?.Camera == null) return;
			var (pos, rot) = orbitController.GetCameraTransform();
			previewCtrl.ApplyExternalCameraTransform(pos, rot);
		}

		private void CleanupPreview()
		{
			if (previewCtrl != null)
			{
				orbitController.OnTransformChanged -= ApplyCameraTransform;
				previewCtrl.Dispose();
				previewCtrl = null;
			}
			currentModelData = null;
		}

		private void Update()
		{
			if (previewCtrl == null) return;
			orbitController.Update();
			previewCtrl.UpdateAndRender();
		}

		// ── Flag Creation ───────────────────────────────────────────────────────────────────

		private void CreateFlagToggles()
		{
			for (int i = flagPropertiesRect.childCount - 1; i >= 0; i--)
				Destroy(flagPropertiesRect.GetChild(i).gameObject);

			spawnedFlagControls.Clear();

			foreach (var flag in AllFlags)
			{
				var instance = Instantiate(flagTogglePrefab, flagPropertiesRect);
				var toggle = instance.GetComponent<Toggle>();
				var label = instance.GetComponentInChildren<TMP_Text>();

				if (toggle == null || label == null)
				{
					Destroy(instance);
					continue;
				}

				label.text = flag.DisplayName;
				toggle.isOn = false;

				spawnedFlagControls.Add((toggle, flag));

				toggle.onValueChanged.AddListener(isOn =>
				{
					var def = CurrentDefinition;
					if (def == null) return;
					flag.SetValue(def, isOn);
				});
			}
		}

		// ── Definition CRUD Operations ──────────────────────────────────────────────────────

		private void InsertDefinition()
		{
			if (ResourceManager.database == null) return;

			string newId = ResourceManager.GenerateUniqueNewDefinitionId();
			//var def = Definition.GetDefault(newId);
			var def = ResourceManager.CreateDefinition(newId);

			int insertIndex = lastSelectedDefinitionIndex >= 0 ? lastSelectedDefinitionIndex + 1 : 0;
			ResourceManager.InsertDefinitionAt(insertIndex, def);

			lastSelectedDefinitionIndex = insertIndex;
			RefreshDefinitionList();
		}

		private void DeleteDefinition()
		{
			if (ResourceManager.database == null || lastSelectedDefinitionIndex < 0) return;

			int indexToDelete = lastSelectedDefinitionIndex;

			ResourceManager.DeleteDefinitionAt(indexToDelete);

			var defs = ResourceManager.Definitions;
			if (defs.Count == 0)
			{
				lastSelectedDefinitionIndex = -1;
			}
			else
			{
				lastSelectedDefinitionIndex = Mathf.Max(0, indexToDelete - 1);
				if (lastSelectedDefinitionIndex >= defs.Count)
					lastSelectedDefinitionIndex = defs.Count - 1;
			}

			RefreshDefinitionList();
		}

		private void MoveDefinitionUp()
		{
			if (lastSelectedDefinitionIndex <= 0) return;

			ResourceManager.MoveDefinitionUp(lastSelectedDefinitionIndex);
			lastSelectedDefinitionIndex--;
			RefreshDefinitionList();
		}

		private void MoveDefinitionDown()
		{
			if (lastSelectedDefinitionIndex < 0 || lastSelectedDefinitionIndex >= ResourceManager.Definitions.Count - 1) return;

			ResourceManager.MoveDefinitionDown(lastSelectedDefinitionIndex);
			lastSelectedDefinitionIndex++;
			RefreshDefinitionList();
		}
	}
}