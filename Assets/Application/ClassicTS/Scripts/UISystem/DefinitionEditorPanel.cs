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

		[Header("Model Selection")]
		[SerializeField] private TMP_Dropdown modelDropdown;
		[SerializeField] private string noneModelOptionText = "— None —";

		[Header("Texture Selection")]
		[SerializeField] private TMP_Dropdown textureDropdown;
		[SerializeField] private string noneTextureOptionText = "— None —";

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

		// Runtime
		private readonly List<Toggle> spawnedDefinitionToggles = new();
		private ToggleGroup toggleGroup;
		private string selectedDefinitionId;

		private PreviewSceneController previewCtrl;
		private CommandRenderModelData currentModelData;
		private GimbalOrbitController orbitController;

		private Definition CurrentDefinition =>
			string.IsNullOrEmpty(selectedDefinitionId) ? null :
			ResourceManager.GetDefinition(selectedDefinitionId);

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

			if (closeButton) closeButton.onClick.AddListener(() => gameObject.SetActive(false));

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

			CreateFlagToggles();

			if (modelDropdown != null)
			{
				modelDropdown.onValueChanged.AddListener(OnModelDropdownValueChanged);
			}

			if (IDInput != null)
			{
				// We want to react when user FINISHES editing (presses Enter or focus lost)
				IDInput.onEndEdit.AddListener(OnIDInputEndEdit);
			}

			if (textureDropdown != null)
			{
				textureDropdown.onValueChanged.AddListener(OnTextureDropdownValueChanged);
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			EnsurePreviewInitialized();
			RefreshDefinitionList();
			PopulateModelDropdown();
			PopulateTextureDropdown();
			StartCoroutine(DelayedInitialSync());
		}

		protected override void OnDisable()
		{
			CleanupPreview();
			ClearDefinitionListItems();
			base.OnDisable();
		}

		private System.Collections.IEnumerator DelayedInitialSync()
		{
			yield return null;
			SyncModelDropdown();
		}

		private void OnIDInputEndEdit(string newId)
		{
			if (CurrentDefinition == null) return;
			if (string.IsNullOrWhiteSpace(newId))
			{
				// Optional: revert to old value if user cleared it
				IDInput.text = selectedDefinitionId;
				return;
			}

			newId = newId.Trim();

			// Same ID → nothing to do
			if (newId == selectedDefinitionId)
				return;

			// Check if target ID already exists
			if (ResourceManager.Definitions.Any(d => d.id == newId))
			{
				// Optional: nice feedback
				Debug.LogWarning($"ID '{newId}' already exists!");
				IDInput.text = selectedDefinitionId; // revert
													 // You could also show a popup/warning here
				return;
			}

			// ── The actual rename ───────────────────────────────────────
			string oldId = selectedDefinitionId;

			CurrentDefinition.id = newId;
			selectedDefinitionId = newId;

			// Optional but recommended: update preview & list immediately
			RefreshDefinitionList();           // rebuilds whole list + selects new id
											   // or lighter version:
											   // UpdateListItemLabel(oldId, newId);
											   // (you'd need to implement that separately if you want to avoid full refresh)
		}

		private void OnTextureDropdownValueChanged(int index)
		{
			if (index < 0 || index >= textureDropdown.options.Count) return;

			var def = CurrentDefinition;
			if (def == null) return;

			string selectedText = textureDropdown.options[index].text;
			string newTexture = (selectedText == noneTextureOptionText) ? null : selectedText;

			if (newTexture != def.texture)
			{
				def.texture = newTexture;
				UpdatePreview(selectedDefinitionId);
			}
		}

		private void SyncModelDropdown()
		{
			if (modelDropdown == null || modelDropdown.options.Count == 0)
				return;

			var def = CurrentDefinition;
			int targetIndex = 0;

			if (def != null && !string.IsNullOrEmpty(def.model))
			{
				string modelClean = def.model.Clean();

				targetIndex = modelDropdown.options.FindIndex(opt =>
					opt.text.CleanEquals(modelClean)
				);

				if (targetIndex < 0) targetIndex = 0;
			}

			modelDropdown.SetValueWithoutNotify(targetIndex);
		}

		private void PopulateModelDropdown()
		{
			if (modelDropdown == null) return;

			modelDropdown.ClearOptions();

			var modelNames = ProjectAssets.GetModelNames();

			var options = new List<string> { noneModelOptionText };
			options.AddRange(modelNames);

			modelDropdown.AddOptions(options);
			modelDropdown.interactable = true;
		}

		private void OnModelDropdownValueChanged(int index)
		{
			if (index < 0 || index >= modelDropdown.options.Count) return;

			var def = CurrentDefinition;
			if (def == null) return;

			string selectedText = modelDropdown.options[index].text;
			string newModel = (selectedText == noneModelOptionText) ? null : selectedText;

			// ── Important change happens here ───────────────────────────────
			bool modelActuallyChanged = newModel != def.model;

			if (modelActuallyChanged)
			{
				def.model = newModel;
				def.texture = null;
				UpdatePreview(selectedDefinitionId);
				SyncTextureDropdown();
			}
		}

		private void SyncTextureDropdown()
		{
			if (textureDropdown == null || textureDropdown.options.Count == 0)
				return;

			var def = CurrentDefinition;
			int targetIndex = 0;

			if (def != null && !string.IsNullOrEmpty(def.texture))
			{
				string textureClean = def.texture.Clean(); // assuming you have .Clean() extension

				targetIndex = textureDropdown.options.FindIndex(opt =>
					opt.text.CleanEquals(textureClean)
				);

				if (targetIndex < 0) targetIndex = 0;
			}

			textureDropdown.SetValueWithoutNotify(targetIndex);
		}

		private void PopulateTextureDropdown()
		{
			if (textureDropdown == null) return;

			textureDropdown.ClearOptions();

			// Assuming you have some way to get all available texture/sequence names
			// This is just an example — adjust to your real source
			var textureNames = new List<string>();

			foreach (var ts in ResourceManager.TextureSequences)
			{
				if (!string.IsNullOrEmpty(ts.id))
					textureNames.Add(ts.id);
			}

			// Or if you load from Resources or elsewhere...
			// var texAssets = Resources.LoadAll<Texture2D>("Textures/");
			// foreach (var tex in texAssets) textureNames.Add(tex.name);

			var uniqueSorted = textureNames
				.Distinct()
				.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
				.ToList();

			var options = new List<string> { noneTextureOptionText };
			options.AddRange(uniqueSorted);

			textureDropdown.AddOptions(options);
			textureDropdown.interactable = true;
		}

		private void Update()
		{
			if (previewCtrl == null) return;
			orbitController.Update();
			previewCtrl.UpdateAndRender();
		}

		private void EnsurePreviewInitialized()
		{
			if (previewCtrl != null) return;

			if (previewImage == null)
			{
				Debug.LogError("[DefinitionEditorPanel] previewImage is not assigned!", this);
				return;
			}

			InitializePreview();

			if (previewCtrl == null)
			{
				Debug.LogError("[DefinitionEditorPanel] Failed to initialize PreviewSceneController!", this);
				return;
			}
		}

		private void InitializePreview()
		{
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

		private void RefreshDefinitionList()
		{
			ClearDefinitionListItems();

			if (ResourceManager.Definitions.Count == 0)
			{
				selectedDefinitionId = null;
				SyncAllProperties();
				return;
			}

			foreach (var def in ResourceManager.Definitions)
				CreateDefinitionListItem(def);

			string targetId = selectedDefinitionId ?? ResourceManager.Definitions.FirstOrDefault()?.id;

			SetToggleById(targetId);
			UpdateDeleteButtonState();

			// ── This is the magic line ────────────────────────────
			definitionScrollView.GetComponent<ScrollViewKeyboardNavigator>()?.ClearAndRebuild();
		}

		private void CreateDefinitionListItem(Definition def)
		{
			var go = Instantiate(definitionListItemPrefab, contentParent);
			var toggle = go.GetComponent<Toggle>();
			if (toggle == null) { Destroy(go); return; }

			toggle.group = toggleGroup;
			spawnedDefinitionToggles.Add(toggle);

			toggle.onValueChanged.AddListener(isOn =>
			{
				if (isOn) SelectDefinition(def.id);
			});

			var label = go.GetComponentInChildren<TMP_Text>();
			if (label != null) label.text = $"{def.id} ({def.model ?? "—"})";

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
			if (string.IsNullOrEmpty(defId)) return;

			foreach (var t in spawnedDefinitionToggles)
			{
				if (t == null) continue;

				var label = t.GetComponentInChildren<TMP_Text>();
				if (label != null && label.text?.StartsWith(defId) == true)
				{
					t.SetIsOnWithoutNotify(true);
					SelectDefinition(defId);
					return;
				}
			}

			selectedDefinitionId = null;
			IDInput.text = string.Empty;
			SyncAllProperties();
		}

		private void SelectDefinition(string defId)
		{
			selectedDefinitionId = defId;
			IDInput.text = defId;
			UpdatePreview(defId);
			SyncAllProperties();
			SyncModelDropdown();
			SyncTextureDropdown();
			UpdateDeleteButtonState();
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

		private void UpdatePreview(string defId)
		{
			if (previewCtrl == null)
			{
				EnsurePreviewInitialized();
				if (previewCtrl == null) return;
			}

			currentModelData = null;
			previewCtrl.ClearModel();

			var def = ResourceManager.GetDefinition(defId);
			if (def != null && !string.IsNullOrEmpty(def.model))
			{
				currentModelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);
				if (currentModelData != null)
				{
					previewCtrl.SetModel(currentModelData);
					orbitController.ResetView(true, currentModelData.bounds);
				}
				else orbitController.ResetView(false);
			}
			else orbitController.ResetView(false);
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

				if (toggle == null || label == null) { Destroy(instance); continue; }

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

		private void InsertDefinition()
		{
			if (ResourceManager.database == null) return;

			// Much cleaner now:
			string newId = ResourceManager.GenerateUniqueNewDefinitionId();

			var def = Definition.GetDefault(newId);

			ResourceManager.InsertDefinitionAfter(selectedDefinitionId, def);

			selectedDefinitionId = def.id;
			RefreshDefinitionList();
		}

		private void DeleteDefinition()
		{
			if (ResourceManager.database == null) return;

			int indexBeforeDelete = GetSelectedIndex();

			ResourceManager.DeleteDefinition(selectedDefinitionId);

			var defs = ResourceManager.Definitions.ToList();
			if (defs.Count == 0)
			{
				selectedDefinitionId = null;
			}
			else
			{
				// Prefer the item above if possible
				int newIndex = Mathf.Max(0, indexBeforeDelete - 1);
				if (newIndex >= defs.Count) newIndex = defs.Count - 1;
				selectedDefinitionId = defs[newIndex].id;
			}

			RefreshDefinitionList();

			// This helps the toggle UI stay in sync
			if (!string.IsNullOrEmpty(selectedDefinitionId))
			{
				SetToggleById(selectedDefinitionId);
			}

			definitionScrollView.GetComponent<ScrollViewKeyboardNavigator>()?.ClearAndRebuild();
		}

		// Add this new method
		private void UpdateDeleteButtonState()
		{
			if (ButtonDelete == null) return;

			bool hasSelection = !string.IsNullOrEmpty(selectedDefinitionId);
			bool isUsed = hasSelection && ResourceManager.IsDefinitionUsed(selectedDefinitionId);

			ButtonDelete.interactable = hasSelection && !isUsed;

			// Optional: better UX — change text or tooltip when disabled
			if (ButtonDelete.GetComponentInChildren<TMP_Text>() is TMP_Text btnText)
			{
				btnText.text = isUsed ? "Delete (used)" : "Delete";
			}
		}

		private void MoveDefinition(Action<string> moveAction)
		{
			if (ResourceManager.database == null) return;
			moveAction?.Invoke(selectedDefinitionId);
			RefreshDefinitionList();
		}

		private void MoveDefinitionUp() => MoveDefinition(ResourceManager.MoveDefinitionUp);
		private void MoveDefinitionDown() => MoveDefinition(ResourceManager.MoveDefinitionDown);

		private int GetSelectedIndex()
		{
			if (string.IsNullOrEmpty(selectedDefinitionId)) return -1;
			for (int i = 0; i < ResourceManager.Definitions.Count; i++)
				if (ResourceManager.Definitions[i].id == selectedDefinitionId)
					return i;
			return -1;
		}
	}
}
