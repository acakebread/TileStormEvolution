using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class DatabaseEditorPanel : UIPanel
	{
		#region Serialized Fields - UI References

		[Header("UI References")]
		[SerializeField] private GameObject previewCameraPrefab;

		[SerializeField] private Button closeButton;
		[SerializeField] private ScrollRect mapScrollView;
		[SerializeField] private Transform contentParent;
		[SerializeField] private GameObject mapListItemPrefab;

		[SerializeField] private Button ButtonInsert;
		[SerializeField] private Button ButtonDelete;
		[SerializeField] private Button ButtonMoveUp;
		[SerializeField] private Button ButtonMoveDown;

		[SerializeField] private TMP_InputField mapNameInput;

		[Header("Preview")]
		[SerializeField] private RawImage previewImage;

		[Header("Dropdowns")]
		[SerializeField] private TMP_Dropdown skyboxDropdown;
		[SerializeField] private string noneSkyboxOptionText = "— Default —";

		[SerializeField] private TMP_Dropdown characterDropdown;
		[SerializeField] private string noneCharacterOptionText = "— Default —";

		[Header("Colour Pickers")]
		[SerializeField] private RawImage colourPickerImage;     // rainbow hue + saturation square
		[SerializeField] private RawImage brightnessPickerImage; // value/brightness slider
		[SerializeField] private RawImage swatchImage;           // ← the preview swatch that shows the final color

		#endregion

		#region Preview Settings (replacing old orbit & camera section)

		[Header("Preview Settings – Map View")]
		//[SerializeField] private Color backgroundColor = new Color(0.129f, 0.698f, 0.882f);
		[SerializeField] private float fieldOfView = 50f;
		[SerializeField] private float sizeToDistanceFactor = 0.8f;      // tuned for map scale
		[SerializeField] private float defaultTiltAngle = 35f;
		[SerializeField] private float minTiltAngle = 0f;
		[SerializeField] private float maxTiltAngle = 90f;
		[SerializeField] private float minDistance = 2f;
		[SerializeField] private float maxDistance = 120f;
		[SerializeField] private float dragOrbitSensitivity = 0.25f;
		[SerializeField] private float dragTiltSensitivity = 0.20f;
		[SerializeField] private float scrollZoomSensitivity = 0.6f;
		[SerializeField] private float autoRotateSpeed = -12f;

		#endregion

		// Color picker state
		private Texture2D colorTexture;
		private Texture2D valueTexture;
		private float currentHue = 0f;
		private float currentSaturation = 0.8f;
		private float currentValue = 1f;
		private UIDragHandler colorDrag;
		private UIDragHandler valueDrag;

		private readonly List<Toggle> spawnedMapToggles = new List<Toggle>();
		private ToggleGroup toggleGroup;
		private static int lastSelectedMapIndex = 0;

		private Map CurrentMap =>
			lastSelectedMapIndex >= 0 && lastSelectedMapIndex < ResourceManager.Maps.Count
				? ResourceManager.Maps[lastSelectedMapIndex]
				: null;

		private GameObject currentPreviewInstance;

		// Gimbal orbit controller (replaces old auto-orbit)
		private GimbalOrbitController orbitController;

		protected override void Awake()
		{
			base.Awake();
			InitializeUIReferences();
		}

		protected override void OnEnable()
		{
			base.OnEnable();

			// ── Color picker drag wiring ───────────────────────────────────────
			colorDrag = colourPickerImage?.GetComponent<UIDragHandler>();
			valueDrag = brightnessPickerImage?.GetComponent<UIDragHandler>();

			if (colorDrag != null)
			{
				colorDrag.OnPointerDownEvent += OnColorPointer;
				colorDrag.OnDragEvent += OnColorPointer;
			}

			if (valueDrag != null)
			{
				valueDrag.OnPointerDownEvent += OnValuePointer;
				valueDrag.OnDragEvent += OnValuePointer;
			}

			// ── Critical: recreate & assign color picker textures (this was missing) ──
			if (colourPickerImage != null && brightnessPickerImage != null)
			{
				colorTexture = ColorPickerSquareUtility.CreateColorPickerTexture(
					size: 256,
					style: ColorPickerSquareUtility.PickerStyle.HueSaturation_FullValue
				);
				colourPickerImage.texture = colorTexture;

				UpdateValueSlider();  // creates brightness slider based on initial hue/sat
			}

			// ── Rest of your OnEnable ──────────────────────────────────────────
			InitializeOrbitController();
			SetupPreviewInput();

			RefreshMapList();
			PopulateSkyboxDropdown();
			PopulateCharacterDropdown();

			SyncColorPickerToCurrentMap();
			SyncSkyboxDropdown();
			SyncCharacterDropdown();

			MapPreviewUtil.Initialize(CurrentMap, previewCameraPrefab);
			MapPreviewUtil.UpdateRenderTextureSizeIfNeeded();

			if (previewImage != null)
				MapPreviewUtil.SetPreviewUI(previewImage, previewImage.rectTransform);

			SetSkybox(CurrentMap?.Skybox);

			UpdateMapPreview();
		}

		protected override void OnDisable()
		{
			if (colorDrag != null)
			{
				colorDrag.OnPointerDownEvent -= OnColorPointer;
				colorDrag.OnDragEvent -= OnColorPointer;
			}

			if (valueDrag != null)
			{
				valueDrag.OnPointerDownEvent -= OnValuePointer;
				valueDrag.OnDragEvent -= OnValuePointer;
			}

			if (orbitController != null)
			{
				orbitController.OnTransformChanged -= ApplyOrbitToPreviewCamera;
				orbitController = null;
			}

			ClearMapListItems();

			if (currentPreviewInstance != null)
			{
				DestroyImmediate(currentPreviewInstance);
				currentPreviewInstance = null;
			}

			if (currentPreviewRoot != null)
			{
				DestroyImmediate(currentPreviewRoot);
				currentPreviewRoot = null;
			}

			if (currentPreviewInstance != null)
			{
				// usually already destroyed via currentPreviewRoot, but safe
				DestroyImmediate(currentPreviewInstance);
				currentPreviewInstance = null;
			}

			MapPreviewUtil.Cleanup();

			if (previewImage != null)
				previewImage.texture = null;

			if (colorTexture != null) Destroy(colorTexture);
			if (valueTexture != null) Destroy(valueTexture);

			base.OnDisable();
		}

		private void Update()
		{
			if (!isActiveAndEnabled) return;

			MapPreviewUtil.UpdateRenderTextureSizeIfNeeded();

			orbitController?.Update();
			// No need for manual camera update – handled by OnTransformChanged callback
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Orbit Controller Setup
		// ────────────────────────────────────────────────────────────────────────────────

		private void InitializeOrbitController()
		{
			orbitController = new GimbalOrbitController(
				dragOrbitSens: dragOrbitSensitivity,
				dragTiltSens: dragTiltSensitivity,
				scrollZoomSens: scrollZoomSensitivity,
				minTilt: minTiltAngle,
				maxTilt: maxTiltAngle,
				minDist: minDistance,
				maxDist: maxDistance,
				defaultTilt: defaultTiltAngle,
				sizeToDistFactor: sizeToDistanceFactor
			)
			{
				AutoRotateSpeed = autoRotateSpeed,
				AutoRotateTimeout = 3f,
				EnableInertia = true
			};

			// Map-specific: pivot at center Y = 0 (or slightly below)
			orbitController.PivotOffset = new Vector3(0, -0.5f, 0);   // or 0, 0, 0 — tune as needed

			orbitController.OnTransformChanged += ApplyOrbitToPreviewCamera;
		}


		private void SetupPreviewInput()
		{
			if (previewImage == null) return;

			if (!previewImage.TryGetComponent<PointerDragScrollHandler>(out var handler))
				handler = previewImage.gameObject.AddComponent<PointerDragScrollHandler>();

			handler.Setup(
				onDrag: orbitController.ProcessDrag,
				onScroll: orbitController.ProcessScroll,
				onUp: orbitController.EndDrag
			);
		}

		private void ApplyOrbitToPreviewCamera()
		{
			var cam = MapPreviewUtil.PreviewCamera;
			if (cam == null || orbitController == null) return;

			var (pos, rot) = orbitController.GetCameraTransform();
			cam.transform.SetPositionAndRotation(pos, rot);
			cam.fieldOfView = fieldOfView;
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Color Picker (unchanged)
		// ────────────────────────────────────────────────────────────────────────────────

		private void OnColorPointer(UIDragHandler sender, PointerEventData eventData)
		{
			UpdateColorFromPointer(eventData, colourPickerImage.rectTransform, colorTexture, true);
		}

		private void OnValuePointer(UIDragHandler sender, PointerEventData eventData)
		{
			UpdateColorFromPointer(eventData, brightnessPickerImage.rectTransform, valueTexture, false);
		}

		private void UpdateColorFromPointer(PointerEventData eventData, RectTransform rt, Texture2D tex, bool isColorSquare)
		{
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out Vector2 localPos))
				return;

			Color picked = ColorPickerSquareUtility.GetColorFromLocalPoint(tex, localPos, rt);

			if (isColorSquare)
			{
				Color.RGBToHSV(picked, out currentHue, out currentSaturation, out _);
				UpdateValueSlider();
			}
			else
			{
				Color.RGBToHSV(picked, out _, out _, out currentValue);
			}

			Color final = Color.HSVToRGB(currentHue, currentSaturation, currentValue);

			if (swatchImage != null)
				swatchImage.color = final;

			CurrentMap.Light = final;

			MapPreviewUtil.UpdateOverrideSettings(MapPreviewUtil.CreateRenderSettingsFromMap(CurrentMap));
		}

		private void UpdateValueSlider()
		{
			if (valueTexture != null) Destroy(valueTexture);
			valueTexture = ColorPickerSquareUtility.CreateValueSliderTexture(
				height: 256,
				hue: currentHue,
				saturation: currentSaturation
			);
			brightnessPickerImage.texture = valueTexture;
		}

		private void SyncColorPickerToCurrentMap()
		{
			if (CurrentMap == null)
			{
				currentHue = 0f;
				currentSaturation = 0.8f;
				currentValue = 1f;
				UpdateValueSlider();
				if (swatchImage != null) swatchImage.color = Color.white;
				//SetLightColour(Color.white);
				return;
			}

			Color lightColor = CurrentMap.Light;
			Color.RGBToHSV(lightColor, out currentHue, out currentSaturation, out currentValue);
			UpdateValueSlider();

			if (swatchImage != null)
				swatchImage.color = lightColor;
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Map List & Selection (mostly unchanged)
		// ────────────────────────────────────────────────────────────────────────────────

		private void InitializeUIReferences()
		{
			if (ButtonInsert) ButtonInsert.onClick.AddListener(InsertMap);
			if (ButtonDelete) ButtonDelete.onClick.AddListener(DeleteMap);
			if (ButtonMoveUp) ButtonMoveUp.onClick.AddListener(MoveMapUp);
			if (ButtonMoveDown) ButtonMoveDown.onClick.AddListener(MoveMapDown);

			if (closeButton != null)
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));

			if (contentParent == null && mapScrollView != null)
				contentParent = mapScrollView.content;

			toggleGroup = contentParent.GetComponent<ToggleGroup>()
				?? contentParent.gameObject.AddComponent<ToggleGroup>();
			toggleGroup.allowSwitchOff = false;

			if (mapNameInput != null) mapNameInput.onEndEdit.AddListener(OnMapNameChanged);

			if (skyboxDropdown != null)
				skyboxDropdown.onValueChanged.AddListener(OnSkyboxDropdownValueChanged);

			if (characterDropdown != null)
				characterDropdown.onValueChanged.AddListener(OnCharacterDropdownValueChanged);
		}

		private void OnMapNameChanged(string input)
		{
			if (CurrentMap == null) return;

			string newName = (input ?? "").Trim();

			if (string.IsNullOrWhiteSpace(newName) || newName == CurrentMap.name)
			{
				mapNameInput.text = CurrentMap.name;
				return;
			}

			bool result = ResourceManager.RenameMapName(CurrentMap, newName);
			RefreshMapList();
		}

		private void OnSkyboxDropdownValueChanged(int index)
		{
			if (CurrentMap == null) return;

			string selected = index >= 0 && index < skyboxDropdown.options.Count
				? skyboxDropdown.options[index].text
				: null;

			string newSkybox = (selected == noneSkyboxOptionText) ? null : selected;

			if (newSkybox != CurrentMap.Skybox)
			{
				CurrentMap.Skybox = newSkybox;
				SetSkybox(newSkybox);
			}
		}

		private void OnCharacterDropdownValueChanged(int index)
		{
			if (CurrentMap == null) return;

			string selected = index >= 0 && index < characterDropdown.options.Count
				? characterDropdown.options[index].text
				: null;

			string newCharacter = (selected == noneCharacterOptionText) ? null : selected;

			if (newCharacter != CurrentMap.character)
				CurrentMap.character = newCharacter;
		}

		private void RefreshMapList()
		{
			ClearMapListItems();

			var maps = ResourceManager.Maps;
			if (maps.Count == 0)
			{
				lastSelectedMapIndex = -1;
				UpdateMapPreview();
				return;
			}

			for (int i = 0; i < maps.Count; i++)
				CreateMapListItem(maps[i], i);

			SetSelectedMapIndex(lastSelectedMapIndex);
		}

		private void CreateMapListItem(Map map, int index)
		{
			if (map == null) return;

			var go = Instantiate(mapListItemPrefab, contentParent);
			var toggle = go.GetComponent<Toggle>();
			if (toggle == null)
			{
				Destroy(go);
				return;
			}

			toggle.group = toggleGroup;
			spawnedMapToggles.Add(toggle);

			toggle.onValueChanged.AddListener(isOn =>
			{
				if (isOn) SetSelectedMapIndex(index);
			});

			var label = go.GetComponentInChildren<TMP_Text>();
			if (label != null)
			{
				string display = map.name ?? "Unnamed";
				if (map.width > 0 && map.height > 0)
					display += $"  ({map.width}×{map.height})";

				label.text = display;
			}
		}

		private void ClearMapListItems()
		{
			foreach (var t in spawnedMapToggles)
				if (t != null) Destroy(t.gameObject);

			spawnedMapToggles.Clear();
		}

		private void SetSelectedMapIndex(int index)
		{
			index = Mathf.Clamp(index, -1, ResourceManager.Maps.Count - 1);
			lastSelectedMapIndex = index;

			var map = CurrentMap;
			mapNameInput.text = map?.name ?? "";

			UpdateDeleteButtonState();

			if (index >= 0 && index < spawnedMapToggles.Count)
				spawnedMapToggles[index].SetIsOnWithoutNotify(true);

			SyncColorPickerToCurrentMap();
			SyncSkyboxDropdown();
			SyncCharacterDropdown();

			MapPreviewUtil.SetActiveMap(map);
			UpdateMapPreview();
			SetSkybox(map?.Skybox);
		}

		private void UpdateDeleteButtonState()
		{
			if (ButtonDelete == null) return;
			bool hasSelection = lastSelectedMapIndex >= 0;
			ButtonDelete.interactable = hasSelection;

			if (ButtonDelete.GetComponentInChildren<TMP_Text>() is TMP_Text txt)
			{
				txt.text = "Delete";
			}
		}

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
			int index = dropdown.options.FindIndex(opt => opt.text.Equals(currentValue, StringComparison.OrdinalIgnoreCase));
			dropdown.SetValueWithoutNotify(index >= 0 ? index : 0);
		}

		private void PopulateSkyboxDropdown() =>
			PopulateDropdown(skyboxDropdown, Assets.ProjectAssets.GetSkycubeNames(), noneSkyboxOptionText);

		private void SyncSkyboxDropdown() =>
			SyncDropdown(skyboxDropdown, CurrentMap?.Skybox, noneSkyboxOptionText);

		private void PopulateCharacterDropdown()
		{
			var characterNames = new List<string>
			{
				"Eggbot Default", "Eggbot Industrial", "Eggbot Egypt",
				"Eggbot Medieval", "Eggbot Jungle",
			};
			PopulateDropdown(characterDropdown, characterNames, noneCharacterOptionText);
		}

		private void SyncCharacterDropdown() =>
			SyncDropdown(characterDropdown, CurrentMap?.character, noneCharacterOptionText);

		// ────────────────────────────────────────────────────────────────────────────────
		//   Map CRUD
		// ────────────────────────────────────────────────────────────────────────────────

		private void InsertMap()
		{
			string newName = GenerateUniqueMapName("Map");
			var newMap = new Map(16, 16, newName);

			int insertIndex = (lastSelectedMapIndex >= 0)
				? lastSelectedMapIndex + 1
				: ResourceManager.Maps.Count;

			var list = ResourceManager.Maps.ToList();
			list.Insert(insertIndex, newMap);
			ResourceManager.database.maps = list.ToArray();

			lastSelectedMapIndex = insertIndex;
			RefreshMapList();
		}

		private void DeleteMap()
		{
			if (lastSelectedMapIndex < 0) return;

			int idx = lastSelectedMapIndex;
			var list = ResourceManager.Maps.ToList();
			list.RemoveAt(idx);
			ResourceManager.database.maps = list.ToArray();

			lastSelectedMapIndex = Mathf.Clamp(idx - 1, 0, list.Count - 1);
			RefreshMapList();
		}

		private void MoveMapUp()
		{
			if (lastSelectedMapIndex <= 0) return;

			var list = ResourceManager.Maps.ToList();
			int i = lastSelectedMapIndex;
			(list[i - 1], list[i]) = (list[i], list[i - 1]);
			ResourceManager.database.maps = list.ToArray();

			lastSelectedMapIndex--;
			RefreshMapList();
		}

		private void MoveMapDown()
		{
			var maps = ResourceManager.Maps;
			if (lastSelectedMapIndex < 0 || lastSelectedMapIndex >= maps.Count - 1) return;

			var list = maps.ToList();
			int i = lastSelectedMapIndex;
			(list[i + 1], list[i]) = (list[i], list[i + 1]);
			ResourceManager.database.maps = list.ToArray();

			lastSelectedMapIndex++;
			RefreshMapList();
		}

		private string GenerateUniqueMapName(string prefix = "Map")
		{
			var existing = ResourceManager.Maps
				.Select(m => m?.name)
				.Where(n => !string.IsNullOrEmpty(n))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			string candidate;
			int suffix = 1;
			do
			{
				candidate = $"{prefix} {suffix++}";
			} while (existing.Contains(candidate));

			return candidate;
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Preview Utilities
		// ────────────────────────────────────────────────────────────────────────────────

		private void SetSkybox(string value = null)
		{
			var skyMaterial = SkyboxUtility.GetSkyboxMaterialForName(value);
			if (skyMaterial != null)
				MapPreviewUtil.SetSkyboxOverride(skyMaterial);
		}

		private GameObject currentPreviewRoot;   // ← this is the one GameObject we spawn

		private void UpdateMapPreview()
		{
			if (MapPreviewUtil.PreviewCamera == null || previewImage == null) return;

			var map = CurrentMap;

			// Destroy previous preview (clean, fast, zero scanning)
			if (currentPreviewRoot != null)
			{
				DestroyImmediate(currentPreviewRoot);
				currentPreviewRoot = null;
			}

			if (map == null || map.Width <= 0 || map.Height <= 0)
			{
				previewImage.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
				orbitController?.ResetView(false);
				return;
			}

			previewImage.color = Color.white;

			// Create dedicated root object – named, easy to spot in hierarchy
			currentPreviewRoot = new GameObject($"Preview – {map.name ?? "Untitled"}");
			currentPreviewRoot.transform.SetParent(MapPreviewUtil.PreviewMapRoot ?? transform); // fallback if no util root

			// Optional: reset transform
			currentPreviewRoot.transform.localPosition = Vector3.zero;
			currentPreviewRoot.transform.localRotation = Quaternion.identity;

			// Build under our owned root
			var geometry = map.BuildPreviewGeometry(currentPreviewRoot.transform, PreviewRenderLayers.previewMask);

			if (geometry == null)
			{
				previewImage.color = new Color(1f, 0.3f, 0.3f, 0.7f);
				orbitController?.ResetView(false);
				return;
			}

			// currentPreviewInstance can stay if you still need direct ref to the returned object
			currentPreviewInstance = geometry;

			// bounds calculation + reframing (unchanged)
			var bounds = new Bounds();
			bool hasRenderers = false;

			foreach (var rend in currentPreviewInstance.GetComponentsInChildren<Renderer>())
			{
				if (!hasRenderers) { bounds = rend.bounds; hasRenderers = true; }
				else bounds.Encapsulate(rend.bounds);
			}

			if (!hasRenderers)
			{
				bounds = new Bounds(new Vector3(map.Width * 0.5f, 0, map.Height * 0.5f),
									new Vector3(map.Width, 5f, map.Height));
			}

			MapPreviewUtil.UpdateRenderTextureSizeIfNeeded();
			orbitController?.Reframe(bounds, distanceMultiplier: 1f);
		}
	}
}
