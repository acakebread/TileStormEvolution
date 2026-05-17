using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

		//[SerializeField] private TMP_Dropdown textureSequenceDropdown;
		//[SerializeField] private string noneTextureOptionText = "— None —";

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

		[Header("Preview Models")]
		[SerializeField] private GameObject arrow;

		#endregion

		// Runtime state
		private readonly List<Toggle> spawnedDefinitionToggles = new();
		private readonly Dictionary<Transform, Definition> spawnedDefinitionItems = new();
		private readonly List<string> modelOptionHashes = new();
		private ToggleGroup toggleGroup;
		private ScrollListReorderDragHelper definitionReorderHelper;

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
			new("North",       "Nav North",   d => d.North,       (d, v) => d.North = v),
			new("East",        "Nav East",    d => d.East,        (d, v) => d.East = v),
			new("South",       "Nav South",   d => d.South,       (d, v) => d.South = v),
			new("West",        "Nav West",    d => d.West,        (d, v) => d.West = v),
			new("Bake",        "Is Static",   d => d.Bake,        (d, v) => d.Bake = v),
			new("Roll",        "Roll",        d => d.Roll,        (d, v) => d.Roll = v),
			new("Door",        "Is Door",     d => d.Door,        (d, v) => d.Door = v),
			new("Desk",        "Is Desk",	  d => d.Desk,        (d, v) => d.Desk = v),
			new("Sway",        "Sways",       d => d.Sway,        (d, v) => d.Sway = v),
			new("Wash",        "Bouyant",     d => d.Wash,        (d, v) => d.Wash = v),
			new("Gang",        "Grid Puzzle", d => d.Gang,        (d, v) => d.Gang = v),
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
			EnsureDefinitionReorderHelper();
			PopulateAndSyncDropdowns();
		}

		protected override void OnDisable()
		{
			if (!ApplicationQuit.IsQuitting)
				ResourceManager.OnDefininionsModified?.Invoke();

			CleanupPreview();
			ClearDefinitionListItems();
			definitionReorderHelper?.Dispose();
			definitionReorderHelper = null;
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

		private void EnsureDefinitionReorderHelper()
		{
			definitionReorderHelper ??= new ScrollListReorderDragHelper(
				definitionScrollView,
				contentParent as RectTransform,
				pos => TryGetDefinitionItemUnderPointer(pos, out var row) ? row : null,
				row =>
				{
					if (row != null && spawnedDefinitionItems.TryGetValue(row, out var def))
					{
						var index = ResourceManager.Definitions.ToList().FindIndex(d => ReferenceEquals(d, def));
						if (index >= 0)
							SetSelectedIndex(index);
					}
				},
				CommitDraggedDefinitionOrder);
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

			//if (textureSequenceDropdown != null)
			//	textureSequenceDropdown.onValueChanged.AddListener(OnTextureDropdownValueChanged);

			if (materialDropdown != null)
				materialDropdown.onValueChanged.AddListener(OnMaterialDropdownValueChanged);

			if (IDInput != null)
				IDInput.onEndEdit.AddListener(OnIDInputEndEdit);
		}

		private void PopulateAndSyncDropdowns()
		{
			PopulateModelDropdown();
			//PopulateTextureDropdown();
			PopulateMaterialDropdown();

			SyncModelDropdown();
			//SyncTextureDropdown();
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

		private void PopulateModelDropdown()
		{
			modelOptionHashes.Clear();
			modelOptionHashes.Add(null);

			var entries = ModelAssets.GetModelEntries(forceRefresh: true);
			modelOptionHashes.AddRange(entries.Select(e => e.HashId));

			PopulateDropdown(modelDropdown, entries.Select(e => e.DisplayName), noneModelOptionText);
		}

		//private void PopulateTextureDropdown() =>
		//	PopulateDropdown(textureSequenceDropdown,
		//		ResourceManager.TextureInfos
		//			.Where(ts => !string.IsNullOrEmpty(ts.id))
		//			.Select(ts => ts.id)
		//			.Distinct(StringComparer.OrdinalIgnoreCase)
		//			.OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
		//		noneTextureOptionText);

		private void PopulateMaterialDropdown() =>
			PopulateDropdown(materialDropdown, ProjectAssets.GetMaterialNames(), noneMaterialOptionText);

		private void SyncModelDropdown() =>
			SyncDropdown(modelDropdown, ModelAssets.GetDisplayNameForHash(CurrentDefinition?.model), noneModelOptionText);

		//private void SyncTextureDropdown() =>
		//	SyncDropdown(textureSequenceDropdown, CurrentDefinition?.texture, noneTextureOptionText);

		private void SyncMaterialDropdown() =>
			SyncDropdown(materialDropdown, Assets.MaterialResourceTable.GetDisplayName(CurrentDefinition?.material) ?? CurrentDefinition?.material, noneMaterialOptionText);

		// ── Event Handlers ──────────────────────────────────────────────────────────────────

		private void OnIDInputEndEdit(string input)
		{
			var def = CurrentDefinition;
			if (def == null) return;

			string newName = (input ?? "").Trim();

			if (string.IsNullOrWhiteSpace(newName) || newName == def.name)
			{
				IDInput.text = def.name;
				return;
			}

			if (ResourceManager.Definitions.Any(d => d != def && string.Equals(d.name, newName, StringComparison.Ordinal)))
			{
				Debug.LogWarning($"ID '{newName}' already exists!");
				IDInput.text = def.name;
				return;
			}

			ResourceManager.RenameDefinitionName(def.HashID, newName);
			RefreshDefinitionList();
		}

		private void OnModelDropdownValueChanged(int index)
		{
			var def = CurrentDefinition;
			if (def == null) return;

			string newModel = index > 0 && index < modelOptionHashes.Count
				? modelOptionHashes[index]
				: null;

			if (newModel != def.model)
			{
				def.model = newModel;
				//def.texture = null;
				def.material = null;
				UpdatePreview(lastSelectedDefinitionIndex);
				//SyncTextureDropdown();
				SyncMaterialDropdown();
			}
		}

		//private void OnTextureDropdownValueChanged(int index)
		//{
		//	var def = CurrentDefinition;
		//	if (def == null) return;

		//	string selected = index >= 0 && index < textureSequenceDropdown.options.Count
		//		? textureSequenceDropdown.options[index].text : null;

		//	string newTexture = (selected == noneTextureOptionText) ? null : selected;

		//	if (newTexture != def.texture)
		//	{
		//		def.texture = newTexture;
		//		UpdatePreview(lastSelectedDefinitionIndex);
		//	}
		//}

		private void OnMaterialDropdownValueChanged(int index)
		{
			var def = CurrentDefinition;
			if (def == null) return;

			string selected = index >= 0 && index < materialDropdown.options.Count
				? materialDropdown.options[index].text : null;

			string newMaterial = (selected == noneMaterialOptionText) ? null : Assets.MaterialResourceTable.GetHashForDisplayName(selected) ?? selected;

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
			spawnedDefinitionItems[go.transform] = def;

			toggle.onValueChanged.AddListener(isOn =>
			{
				if (isOn) SetSelectedIndex(index);
			});

			var label = go.GetComponentInChildren<TMP_Text>();
			if (label != null)
			{
				var usage = ResourceManager.DefinitionUsageCount(def.HashID);
				var hashDisplay = 0 == def.HashID ? "(no hashid)" : $"hash: {HTB50Settings.ToString(def.HashID)}";
				label.text = $"{def?.name ?? "???"}  [{usage}]  ({hashDisplay})";
			}
		}

		private void ClearDefinitionListItems()
		{
			foreach (var t in spawnedDefinitionToggles)
				if (t != null) Destroy(t.gameObject);

			spawnedDefinitionToggles.Clear();
			spawnedDefinitionItems.Clear();
		}

		private bool TryGetDefinitionItemUnderPointer(Vector2 screenPos, out Transform row)
		{
			row = null;

			var ped = new PointerEventData(EventSystem.current)
			{
				position = screenPos
			};

			var results = new List<RaycastResult>();
			EventSystem.current.RaycastAll(ped, results);

			foreach (var result in results)
			{
				var current = result.gameObject != null ? result.gameObject.transform : null;
				while (current != null && current != contentParent)
				{
					if (spawnedDefinitionItems.ContainsKey(current))
					{
						row = current;
						return true;
					}

					current = current.parent;
				}
			}

			return false;
		}

		private void CommitDraggedDefinitionOrder(Vector2 pointerPosition, Transform row)
		{
			if (ResourceManager.database == null || contentParent == null || row == null)
				return;

			int targetIndex = CalculateDefinitionDropIndex(pointerPosition, row);
			targetIndex = Mathf.Clamp(targetIndex, 0, Mathf.Max(0, contentParent.childCount - 1));

			if (row.GetSiblingIndex() != targetIndex)
				row.SetSiblingIndex(targetIndex);

			CommitDefinitionOrderFromContent();
		}

		private int CalculateDefinitionDropIndex(Vector2 pointerPosition, Transform draggedItem)
		{
			if (contentParent == null)
				return 0;

			var contentRect = contentParent as RectTransform;
			var canvas = definitionScrollView != null ? definitionScrollView.GetComponentInParent<Canvas>() : null;
			var screenCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRect, pointerPosition, screenCamera, out var localPoint))
				return 0;

			int insertIndex = 0;
			foreach (Transform child in contentParent)
			{
				if (child == null || child == draggedItem)
					continue;

				if (!spawnedDefinitionItems.ContainsKey(child))
					continue;

				if (!child.TryGetComponent<RectTransform>(out var rt))
					continue;

				var childLocalPoint = contentRect.InverseTransformPoint(rt.position);
				if (localPoint.y < childLocalPoint.y)
					insertIndex++;
				else
					break;
			}

			return insertIndex;
		}

		private void CommitDefinitionOrderFromContent()
		{
			if (ResourceManager.database == null || contentParent == null)
				return;

			var selectedDefinition = CurrentDefinition;
			var orderedDefinitions = new List<Definition>(ResourceManager.Definitions.Count);

			foreach (Transform child in contentParent)
			{
				if (child == null)
					continue;

				if (spawnedDefinitionItems.TryGetValue(child, out var def) && def != null)
					orderedDefinitions.Add(def);
			}

			if (orderedDefinitions.Count != ResourceManager.Definitions.Count)
				return;

			ResourceManager.database.definitions = orderedDefinitions.ToArray();
			ResourceManager.OnDefininionsModified?.Invoke();

			if (selectedDefinition != null)
				lastSelectedDefinitionIndex = orderedDefinitions.FindIndex(d => ReferenceEquals(d, selectedDefinition));

			RefreshDefinitionList();
		}

		private void SetSelectedIndex(int index)
		{
			index = Mathf.Clamp(index, -1, ResourceManager.Definitions.Count - 1);
			lastSelectedDefinitionIndex = index;

			var def = CurrentDefinition;

			IDInput.text = def?.name ?? "";

			SyncAllProperties();
			UpdatePreview(index);
			SyncModelDropdown();
			//SyncTextureDropdown();
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
			bool isUsed = hasSelection && ResourceManager.IsDefinitionUsed(CurrentDefinition.HashID);

			ButtonDelete.interactable = hasSelection && !isUsed;

			if (ButtonDelete.GetComponentInChildren<TMP_Text>() is TMP_Text btnText)
			{
				btnText.text = isUsed ? "Delete (used)" : "Delete";
			}
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

			if (previewCtrl != null)
			{
				Canvas.ForceUpdateCanvases();
				previewCtrl.UpdateRenderTextureSizeIfNeeded();
			}

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

		private void SyncLightingToCamera()
		{
			if (previewCtrl?.Scene == null) return;

			previewCtrl.Scene.MainLightDirection = previewCtrl.Camera.rotation * Vector3.back;
			previewCtrl.Scene.MainLightColor = new Color(1.12f, 1.08f, 1.02f);
			previewCtrl.Scene.MainLightIntensity = 1.5f;
			previewCtrl.Scene.AmbientColor = new Color(0.28f, 0.28f, 0.32f);
			previewCtrl.Scene.AmbientIntensity = 0.9f;
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
			previewCtrl.ClearAdditionalModels();

			var def = (index >= 0 && index < ResourceManager.Definitions.Count)
				? ResourceManager.Definitions[index]
				: null;

			if (def != null && !string.IsNullOrEmpty(def.model))
			{
				currentModelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);

				if (currentModelData != null)
				{
					previewCtrl.SetModel(currentModelData);

					float lowestY = currentModelData.bounds.min.y;
					previewCtrl.GroundY = lowestY + groundY;

					orbitController.ResetView(true, currentModelData.bounds);
				}
				else
				{
					orbitController.ResetView(false);
				}

				if (arrow != null && def != null)
				{
					AddNavigationArrows(def);
				}
			}
			else
			{
				previewCtrl.GroundY = groundY;
				orbitController.ResetView(false);
			}

			SyncLightingToCamera();
		}

		/// <summary>
		/// Adds correctly positioned and rotated navigation arrows for active flags.
		/// Arrows are tinted blue.
		/// </summary>
		private void AddNavigationArrows(Definition def)
		{
			if (arrow == null || def == null) return;

			const float arrowHeight = 0.02f;   // vertical clearance
			const float arrowOffset = 0.8f;    // distance from center

			float baseY = currentModelData?.bounds.min.y ?? 0f;
			Vector3 basePos = new Vector3(0f, baseY + arrowHeight, 0f);

			var scale = Vector3.one * 0.5f;

			Color arrowColor = new Color(0.2f, 0.6f, 1f, 1f); // nice blue

			// North
			if (def.North)
			{
				var pos = basePos + Vector3.forward * arrowOffset;
				var data = CommandRenderModelData.Instantiate(arrow, pos, Quaternion.Euler(0, 0, 0), scale, arrowColor);
				if (data != null) previewCtrl.AddModel(data);
			}

			// East
			if (def.East)
			{
				var pos = basePos + Vector3.right * arrowOffset;
				var data = CommandRenderModelData.Instantiate(arrow, pos, Quaternion.Euler(0, 90, 0), scale, arrowColor);
				if (data != null) previewCtrl.AddModel(data);
			}

			// South
			if (def.South)
			{
				var pos = basePos + Vector3.back * arrowOffset;
				var data = CommandRenderModelData.Instantiate(arrow, pos, Quaternion.Euler(0, 180, 0), scale, arrowColor);
				if (data != null) previewCtrl.AddModel(data);
			}

			// West
			if (def.West)
			{
				var pos = basePos + Vector3.left * arrowOffset;
				var data = CommandRenderModelData.Instantiate(arrow, pos, Quaternion.Euler(0, 270, 0), scale, arrowColor);
				if (data != null) previewCtrl.AddModel(data);
			}
		}

		/// <summary>
		/// Called when a navigation flag toggle changes - refreshes only the arrows
		/// </summary>
		private void RefreshNavigationArrows()
		{
			if (previewCtrl == null) return;

			previewCtrl.ClearAdditionalModels();

			var def = CurrentDefinition;
			if (def != null && arrow != null)
			{
				AddNavigationArrows(def);
			}

			previewCtrl.UpdateAndRender();
		}

		private void ApplyCameraTransform()
		{
			if (previewCtrl?.Camera == null) return;
			var (pos, rot) = orbitController.GetCameraTransform();
			previewCtrl.ApplyExternalCameraTransform(pos, rot);
			SyncLightingToCamera();
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
			definitionReorderHelper?.Update();

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

				bool isNavFlag = flag.InternalName is "North" or "East" or "South" or "West";

				toggle.onValueChanged.AddListener(isOn =>
				{
					var def = CurrentDefinition;
					if (def == null) return;

					flag.SetValue(def, isOn);

					// Live update arrows when navigation flags change
					if (isNavFlag)
						RefreshNavigationArrows();
				});
			}
		}

		// ── Definition CRUD Operations ──────────────────────────────────────────────────────

		private void InsertDefinition()
		{
			if (ResourceManager.database == null) return;

			string newName = ResourceManager.GenerateUniqueNewDefinitionName();
			var def = ResourceManager.CreateDefinition(newName);

			int insertIndex = lastSelectedDefinitionIndex >= 0 ? lastSelectedDefinitionIndex + 1 : 0;
			ResourceManager.InsertDefinitionAtIndex(insertIndex, def);

			lastSelectedDefinitionIndex = insertIndex;
			RefreshDefinitionList();
		}

		private void DeleteDefinition()
		{
			if (ResourceManager.database == null || lastSelectedDefinitionIndex < 0) return;

			int indexToDelete = lastSelectedDefinitionIndex;
			ResourceManager.DeleteDefinitionAt(indexToDelete);

			var defs = ResourceManager.Definitions;
			lastSelectedDefinitionIndex = defs.Count == 0 ? -1 :
				Mathf.Max(0, indexToDelete - 1);

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
