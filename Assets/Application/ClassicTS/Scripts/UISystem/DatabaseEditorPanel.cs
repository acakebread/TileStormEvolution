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
	public class DatabaseEditorPanel : UIPanel//, IPointerDownHandler, IDragHandler, IPointerUpHandler
	{
		#region Serialized Fields - UI References

		[Header("UI References")]
		[SerializeField] private GameObject previewCamerPrefab;

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

		// Color picker state
		private Texture2D colorTexture;   // rainbow square
		private Texture2D valueTexture;   // brightness slider
		private float currentHue = 0f;    // 0 = red start
		private float currentSaturation = 0.8f;
		private float currentValue = 1f;
		private UIDragHandler colorDrag;
		private UIDragHandler valueDrag;

		private enum ActivePicker { None, ColorSquare, ValueSlider }
		private ActivePicker currentActivePicker = ActivePicker.None;

		[Header("Preview Orbit & Camera")]
		[SerializeField] private float orbitSpeed = 18f;
		[SerializeField] private float baseTiltAngle = 25f;
		[SerializeField] private float extraTiltAngle = 10f;
		[SerializeField] private float distanceMultiplier = 0.35f;
		[SerializeField] private float distanceOffset = 0f;
		[SerializeField] private float minDistance = 5f;
		[SerializeField] private float maxDistance = 80f;
		[SerializeField] private float defaultFOV = 60f;

		private float currentOrbitAngle = 45f;
		private Vector3 currentMapCenter = Vector3.zero;
		private float currentDistance = 12f;
		private float currentFOV;

		private readonly List<Toggle> spawnedMapToggles = new List<Toggle>();
		private ToggleGroup toggleGroup;
		private static int lastSelectedMapIndex = 0;

		private Map CurrentMap =>
			lastSelectedMapIndex >= 0 && lastSelectedMapIndex < ResourceManager.Maps.Count
				? ResourceManager.Maps[lastSelectedMapIndex]
				: null;

		private GameObject currentPreviewInstance;

		protected override void Awake()
		{
			base.Awake();
			InitializeUIReferences();
		}

		protected override void OnEnable()
		{
			base.OnEnable();

			colorDrag = colourPickerImage.GetComponent<UIDragHandler>();
			valueDrag = brightnessPickerImage.GetComponent<UIDragHandler>();

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

			RefreshMapList();

			if (previewImage != null)
				MapPreviewUtil.SetPreviewUI(previewImage, previewImage.rectTransform);

			MapPreviewUtil.Initialize(previewCamerPrefab, CurrentMap);

			PopulateSkyboxDropdown();
			PopulateCharacterDropdown();

			SyncSkyboxDropdown();
			SyncCharacterDropdown();

			MapPreviewUtil.UpdateRenderTextureSizeIfNeeded();
			UpdateMapPreview();

			// Initialize rainbow + brightness picker
			if (colourPickerImage != null && brightnessPickerImage != null)
			{
				colorTexture = ColorPickerSquareUtility.CreateColorPickerTexture(
					size: 256,
					style: ColorPickerSquareUtility.PickerStyle.HueSaturation_FullValue
				);
				colourPickerImage.texture = colorTexture;

				UpdateValueSlider();
			}
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

			ClearMapListItems();

			MapPreviewUtil.ClearPreviewMap();

			if (currentPreviewInstance != null)
			{
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
			if (!isActiveAndEnabled || CurrentMap == null) return;

			MapPreviewUtil.UpdateRenderTextureSizeIfNeeded();

			currentOrbitAngle += orbitSpeed * Time.deltaTime;
			currentOrbitAngle %= 360f;

			UpdatePreviewCamera();
		}

		// ── Color Picker Input ──────────────────────────────────────────────────────

		//public void OnPointerDown(PointerEventData eventData)
		//{
		//	if (RectTransformUtility.RectangleContainsScreenPoint(
		//		colourPickerImage.rectTransform, eventData.position, eventData.pressEventCamera))
		//	{
		//		currentActivePicker = ActivePicker.ColorSquare;
		//	}
		//	else if (RectTransformUtility.RectangleContainsScreenPoint(
		//		brightnessPickerImage.rectTransform, eventData.position, eventData.pressEventCamera))
		//	{
		//		currentActivePicker = ActivePicker.ValueSlider;
		//	}
		//	else
		//	{
		//		currentActivePicker = ActivePicker.None;
		//		return;
		//	}

		//	UpdateColorFromPointer(eventData);
		//}

		//public void OnDrag(PointerEventData eventData)
		//{
		//	if (currentActivePicker != ActivePicker.None)
		//	{
		//		UpdateColorFromPointer(eventData);
		//	}
		//}

		//public void OnPointerUp(PointerEventData eventData)
		//{
		//	currentActivePicker = ActivePicker.None;
		//}

		private void OnColorPointer(UIDragHandler sender, PointerEventData eventData)
		{
			UpdateColorFromPointer(eventData, colourPickerImage.rectTransform, colorTexture, true);
		}

		private void OnValuePointer(UIDragHandler sender, PointerEventData eventData)
		{
			UpdateColorFromPointer(eventData, brightnessPickerImage.rectTransform, valueTexture, false);
		}

		private void UpdateColorFromPointer(
			PointerEventData eventData,
			RectTransform rt,
			Texture2D tex,
			bool isColorSquare)
		{
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
				rt, eventData.position, eventData.pressEventCamera, out Vector2 localPos))
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
		}

		//private void UpdateColorFromPointer(PointerEventData eventData)
		//{
		//	Vector2 localPos;
		//	Texture2D tex = null;
		//	RectTransform rt = null;

		//	if (currentActivePicker == ActivePicker.ColorSquare)
		//	{
		//		rt = colourPickerImage.rectTransform;
		//		tex = colorTexture;
		//	}
		//	else if (currentActivePicker == ActivePicker.ValueSlider)
		//	{
		//		rt = brightnessPickerImage.rectTransform;
		//		tex = valueTexture;
		//	}
		//	else return;

		//	if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
		//		rt, eventData.position, eventData.pressEventCamera, out localPos))
		//		return;

		//	Color picked = ColorPickerSquareUtility.GetColorFromLocalPoint(tex, localPos, rt);

		//	if (currentActivePicker == ActivePicker.ColorSquare)
		//	{
		//		Color.RGBToHSV(picked, out currentHue, out currentSaturation, out float _);
		//		UpdateValueSlider();
		//	}
		//	else if (currentActivePicker == ActivePicker.ValueSlider)
		//	{
		//		Color.RGBToHSV(picked, out float _, out float __, out currentValue);
		//	}

		//	Color final = Color.HSVToRGB(currentHue, currentSaturation, currentValue);

		//	if (swatchImage != null)
		//	{
		//		swatchImage.color = final;
		//	}

		//	Debug.Log($"Picked: {final}   (HSV: {currentHue:F3}, {currentSaturation:F3}, {currentValue:F3})");

		//	// → Put your real usage here, example:
		//	// previewImage.color = final;
		//	// someMaterial.color = final;
		//}

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

		// ── The rest of your code (UI, maps, preview, etc.) unchanged ──────────────

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

			Debug.Log($"Map '{CurrentMap.name}' → '{newName}'");

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
			{
				CurrentMap.character = newCharacter;
			}
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
			{
				CreateMapListItem(maps[i], i);
			}

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

			SetSkybox(map.skybox);

			SyncSkyboxDropdown();
			SyncCharacterDropdown();
			UpdateMapPreview();
		}

		private void UpdateDeleteButtonState()
		{
			if (ButtonDelete == null) return;

			bool hasSelection = lastSelectedMapIndex >= 0;
			bool cannotDelete = false;

			ButtonDelete.interactable = hasSelection && !cannotDelete;

			if (ButtonDelete.GetComponentInChildren<TMP_Text>() is TMP_Text txt)
			{
				txt.text = cannotDelete ? "Delete (locked)" : "Delete";
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

			int index = dropdown.options.FindIndex(opt =>
				opt.text.Equals(currentValue, StringComparison.OrdinalIgnoreCase));

			dropdown.SetValueWithoutNotify(index >= 0 ? index : 0);
		}

		private void PopulateSkyboxDropdown() =>
			PopulateDropdown(skyboxDropdown, Assets.ProjectAssets.GetSkycubeNames(), noneSkyboxOptionText);

		private void SyncSkyboxDropdown() =>
			SyncDropdown(skyboxDropdown, CurrentMap?.skybox, noneSkyboxOptionText);

		private void PopulateCharacterDropdown()
		{
			var characterNames = new List<string>
			{
				"Eggbot Default",
				"Eggbot Industrial",
				"Eggbot Egypt",
				"Eggbot Medieval",
				"Eggbot Jungle",
			};

			PopulateDropdown(characterDropdown, characterNames, noneCharacterOptionText);
		}

		private void SyncCharacterDropdown()
		{
			if (CurrentMap == null) return;
			SyncDropdown(characterDropdown, CurrentMap.character, noneCharacterOptionText);
		}

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
			if (list.Count == 0) lastSelectedMapIndex = -1;

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

		private void SetSkybox(string value = null)
		{
			var skyMaterial = SkyboxUtility.GetSkyboxMaterialForName(value);
			if (skyMaterial != null)
				MapPreviewUtil.SetSkyboxOverride(skyMaterial);
		}

		private void UpdatePreviewCamera()
		{
			var cam = MapPreviewUtil.PreviewCamera;
			if (cam == null) return;

			var map = CurrentMap;
			if (map == null) return;

			currentMapCenter = new Vector3(map.Width * 0.5f, 0f, map.Height * 0.5f);

			float diag = Mathf.Sqrt(map.Width * map.Width + map.Height * map.Height);
			float targetDistance = diag * distanceMultiplier + distanceOffset;
			currentDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

			currentFOV = defaultFOV;

			Quaternion orbitRot = Quaternion.Euler(0f, currentOrbitAngle, 0f);
			Quaternion baseTilt = Quaternion.Euler(baseTiltAngle, 0f, 0f);
			Quaternion extraTilt = Quaternion.Euler(extraTiltAngle, 0f, 0f);

			Quaternion finalRotation = orbitRot * baseTilt * extraTilt;

			Vector3 direction = finalRotation * Vector3.back;
			Vector3 camPosition = currentMapCenter + direction * currentDistance;

			cam.transform.SetPositionAndRotation(camPosition, finalRotation);
			cam.fieldOfView = currentFOV;
		}

		private void UpdateMapPreview()
		{
			if (MapPreviewUtil.PreviewCamera == null || previewImage == null)
				return;

			var map = CurrentMap;

			MapPreviewUtil.ClearPreviewMap();
			if (currentPreviewInstance != null)
			{
				DestroyImmediate(currentPreviewInstance);
				currentPreviewInstance = null;
			}

			if (map == null || map.Width <= 0 || map.Height <= 0)
			{
				previewImage.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
				return;
			}

			previewImage.color = Color.white;

			currentPreviewInstance = map.InstantiatePreviewCopy(
				MapPreviewUtil.PreviewMapRoot,
				PreviewRenderLayers.previewMask
			);

			if (currentPreviewInstance == null)
			{
				previewImage.color = new Color(1f, 0.3f, 0.3f, 0.7f);
				return;
			}

			MapPreviewUtil.UpdateRenderTextureSizeIfNeeded();

			currentFOV = defaultFOV;

			UpdatePreviewCamera();
		}
	}
}

