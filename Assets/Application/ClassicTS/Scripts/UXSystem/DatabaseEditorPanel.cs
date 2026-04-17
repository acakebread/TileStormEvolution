using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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

		[SerializeField] private TMP_Dropdown musicDropdown;
		[SerializeField] private string noneMusicOptionText = "— Default —";

		[Header("Colour Pickers")]
		[SerializeField] private Button ambientColourButton;
		[SerializeField] private Toggle ambientColourAutoToggle;
		[SerializeField] private Button directionalColourButton;
		[SerializeField] private Toggle directionalColourAutoToggle;

		[Header("Directional Light Position")]
		[SerializeField] private Button directionalPositionConfigButton;
		[SerializeField] private Toggle directionalPositionConfigAutoToggle;

		[Header("Ground Plane")]
		[SerializeField] private TMP_Dropdown effectDropdown;
		[SerializeField] private string noneEffectOptionText = "— Default —";

		#endregion

		#region Preview Settings – Map View

		[Header("Preview Settings – Map View")]
		[SerializeField] private float fieldOfView = 50f;
		[SerializeField] private float sizeToDistanceFactor = 0.8f;
		[SerializeField] private float defaultTiltAngle = 20f;
		[SerializeField] private float minTiltAngle = 0f;
		[SerializeField] private float maxTiltAngle = 90f;
		[SerializeField] private float minDistance = 2f;
		[SerializeField] private float maxDistance = 120f;
		[SerializeField] private float dragOrbitSensitivity = 0.25f;
		[SerializeField] private float dragTiltSensitivity = 0.20f;
		[SerializeField] private float scrollZoomSensitivity = 0.6f;
		[SerializeField] private float autoRotateSpeed = -12f;

		#endregion

		// ────────────────────────────────────────────────────────────────────────────────
		//   Private Fields
		// ────────────────────────────────────────────────────────────────────────────────

		private readonly List<Toggle> spawnedMapToggles = new List<Toggle>();
		private ToggleGroup toggleGroup;
		private static int lastSelectedMapIndex = 0;

		private Map CurrentMap =>
			lastSelectedMapIndex >= 0 && lastSelectedMapIndex < ResourceManager.Maps.Count
				? ResourceManager.Maps[lastSelectedMapIndex]
				: null;

		private Map currentClone;
		private GameObject currentPreviewRoot;

		private GimbalOrbitController orbitController;

		// Ref-counted tinted cubemap used by the current preview
		private Ref<Cubemap> _previewTintedRef;

		// ────────────────────────────────────────────────────────────────────────────────
		//   Unity Lifecycle
		// ────────────────────────────────────────────────────────────────────────────────

		protected override void Awake()
		{
			base.Awake();
			InitializeUIReferences();
		}

		protected override void OnEnable()
		{
			base.OnEnable();

			ScenePreviewUtil.Initialize(CurrentMap?.RenderSettings ?? default, previewCameraPrefab);
			ScenePreviewUtil.SetPreviewUI(previewImage);

			InitializeOrbitController();
			SetupPreviewInput();

			PopulateSkyboxDropdown();
			PopulateCharacterDropdown();
			PopulateMusicDropdown();
			PopulateEffectDropdown();
			RefreshMapList();

			UpdateMapPreview();
		}

		protected override void OnDisable()
		{
			if (orbitController != null)
			{
				orbitController.OnTransformChanged -= ApplyOrbitToPreviewCamera;
				orbitController = null;
			}

			ClearMapListItems();

			// Clean up preview and release any tinted cubemap
			CleanupPreview();

			ScenePreviewUtil.Cleanup();

			if (previewImage != null)
				previewImage.texture = null;

			UIController.ClosePanel<ColourSelectorPanel>();

			base.OnDisable();
		}

		private void CleanupPreview()
		{
			if (currentClone != null)
			{
				currentClone.Destroy();
				currentClone = null;
			}

			if (currentPreviewRoot != null)
			{
				DestroyImmediate(currentPreviewRoot);
				currentPreviewRoot = null;
			}

			// Release the ref-counted tinted cubemap
			_previewTintedRef?.Set(null);
			_previewTintedRef = null;
		}

		private void Update()
		{
			if (!isActiveAndEnabled) return;

			ScenePreviewUtil.Update();
			orbitController?.Update();
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Dropdowns
		// ────────────────────────────────────────────────────────────────────────────────

		private void PopulateSkyboxDropdown() =>
			PopulateDropdown(skyboxDropdown, Assets.ProjectAssets.GetSkycubeNames(), noneSkyboxOptionText);

		private void SyncSkyboxDropdown() =>
			SyncDropdown(skyboxDropdown, CurrentMap?.Skybox, noneSkyboxOptionText);

		private void OnSkyboxDropdownValueChanged(int index)
		{
			if (currentClone == null) return;

			var selected = index >= 0 && index < skyboxDropdown.options.Count ? skyboxDropdown.options[index].text : null;
			var newSkybox = (selected == noneSkyboxOptionText) ? null : selected;

			if (newSkybox != currentClone.Skybox)
			{
				currentClone.Skybox = newSkybox;
				currentClone.UpdateLighting();
				UpdateMapPreview();
			}
			SyncColorTogglesToCurrentMap();
			SyncColorButtonsToCurrentMap();
		}

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

		private void OnCharacterDropdownValueChanged(int index)
		{
			if (CurrentMap == null) return;

			var selected = index >= 0 && index < characterDropdown.options.Count ? characterDropdown.options[index].text : null;
			var newCharacter = (selected == noneCharacterOptionText) ? null : selected;

			if (newCharacter != CurrentMap.character)
				CurrentMap.character = newCharacter;
		}

		private void PopulateMusicDropdown() =>
			PopulateDropdown(musicDropdown, Assets.ProjectAssets.GetMusicNames(), noneMusicOptionText);

		private void SyncMusicDropdown() =>
			SyncDropdown(musicDropdown, CurrentMap?.music, noneMusicOptionText);

		private void OnMusicDropdownValueChanged(int index)
		{
			if (CurrentMap == null) return;

			var selected = index >= 0 && index < musicDropdown.options.Count ? musicDropdown.options[index].text : null;
			var newMusic = (selected == noneMusicOptionText) ? null : selected;

			if (newMusic != CurrentMap.music)
				CurrentMap.music = newMusic;
		}

		private void PopulateEffectDropdown()
		{
			if (effectDropdown == null) return;

			var effectNames = Enum.GetValues(typeof(ReflectionEffectCamera.EffectMode))
				.Cast<ReflectionEffectCamera.EffectMode>()
				.Where(effect => effect != ReflectionEffectCamera.EffectMode.Null)
				.Select(effect => ReflectionEffectCamera.EffectModeToString(effect))
				.ToList();

			PopulateDropdown(effectDropdown, effectNames, noneEffectOptionText);
		}

		private void SyncEffectDropdown()
		{
			if (CurrentMap == null)
			{
				effectDropdown?.SetValueWithoutNotify(0);
				return;
			}

			SyncDropdown(effectDropdown, ReflectionEffectCamera.EffectModeToString(CurrentMap.Effect), noneEffectOptionText);
		}

		private void OnEffectDropdownValueChanged(int index)
		{
			if (currentClone == null) return;

			LeakDetector.LogSnapshot("BEFORE Effect Dropdown Change");

			var selected = index >= 0 && index < effectDropdown.options.Count ? effectDropdown.options[index].text : null;
			var newEffect = (selected == noneEffectOptionText) ? null : selected;
			currentClone.Effect = ReflectionEffectCamera.ParseEffectMode(newEffect);

			UpdateMapPreview();

			LeakDetector.LogSnapshot("AFTER Effect Dropdown Change");
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Colour Pickers + Directional Position Config
		// ────────────────────────────────────────────────────────────────────────────────

		private void SyncColorTogglesToCurrentMap()
		{
			if (currentClone == null) return;
			ambientColourAutoToggle?.SetIsOnWithoutNotify(null == currentClone.ambient);
			directionalColourAutoToggle?.SetIsOnWithoutNotify(null == currentClone.skyrgb);
			directionalPositionConfigAutoToggle?.SetIsOnWithoutNotify(null == currentClone.skyvec);
		}

		private void SyncColorButtonsToCurrentMap()
		{
			if (currentClone == null) return;
			if (null != ambientColourButton) ambientColourButton.GetComponent<Image>().color = currentClone.AmbientRGB;
			if (null != directionalColourButton) directionalColourButton.GetComponent<Image>().color = currentClone.SkyRGB;
		}

		public void OnColourTogglePressed(Toggle src)
		{
			if (currentClone == null) return;
			if (src == ambientColourAutoToggle) currentClone.ambient = src.isOn ? null : currentClone.AmbientRGB.ToHexString(includeAlpha: true);
			if (src == directionalColourAutoToggle) currentClone.skyrgb = src.isOn ? null : currentClone.SkyRGB.ToHexString(includeAlpha: true);
			currentClone.UpdateLighting();

			SyncColorButtonsToCurrentMap();
			UpdateMapPreview();
		}

		public void OnColourButtonPressed(Button src)
		{
			if (src == ambientColourButton && null != ambientColourAutoToggle && ambientColourAutoToggle.isOn)
				return;

			if (src == directionalColourButton && null != directionalColourAutoToggle && directionalColourAutoToggle.isOn)
				return;

			UIController.ClosePanel<ColourSelectorPanel>();
			var colourPanel = UIController.OpenPanel<ColourSelectorPanel>(closeOthers: false);

			if (null != colourPanel)
			{
				colourPanel.SetInitialColor(src.GetComponent<Image>().color);

				colourPanel.onValueChanged = (selectedColor) =>
				{
					src.GetComponent<Image>().color = selectedColor;
					if (src == ambientColourButton) currentClone.AmbientRGB = selectedColor;
					if (src == directionalColourButton) currentClone.SkyRGB = selectedColor;
					currentClone.UpdateLighting();

					UpdateMapPreview();
				};
			}
		}

		public void OnDirectionalPositionConfigTogglePressed(Toggle src)
		{
			if (null == currentClone) return;

			if (src.isOn)
				currentClone.skyvec = null;
			else
			{
				var skyMat = SkyboxUtility.GetSkyboxMaterialForName(currentClone.skybox);
				currentClone.SkyVec = EquirectangularCubemapUtility.FindLightDirection(CubemapUtility.GetTintedCubemap(skyMat), scanAboveHorizonOnly: true);
			}
			currentClone.UpdateLighting();
			UpdateMapPreview();
		}

		public void OnDirectionalPositionConfigButtonPressed(Button src)
		{
			if (currentClone == null) return;

			UIController.ClosePanel<ColourSelectorPanel>();
			UIController.ClosePanel<TextureCoordEditorPanel>();

			var skyboxPanel = UIController.OpenPanel<TextureCoordEditorPanel>(closeOthers: false);
			if (null != skyboxPanel)
			{
				var skyMat = SkyboxUtility.GetSkyboxMaterialForName(currentClone.skybox);
				var sourceCubemap = CubemapUtility.GetTintedCubemap(skyMat);
				var skyboxTexture = null != sourceCubemap ? EquirectangularCubemapUtility.Create(sourceCubemap, width: 512, height: 256) : null;

				var tex_coord = new Vector2(0.5f, 0.75f);
				if (null != currentClone.skyvec)
					tex_coord = EquirectangularCubemapUtility.DirectionToUV(-currentClone.SkyVec);
				else
					tex_coord = null != skyboxTexture ? ImageProcessing.FindSunUV(skyboxTexture, scanAboveHorizonOnly: true) : new Vector2(0.5f, 0.75f);

				skyboxPanel.SetInitialSkybox(skyboxTexture, tex_coord, onUpdate: normalizedUV =>
				{
					currentClone.SkyVec = -EquirectangularCubemapUtility.UVToDirection(normalizedUV);
					currentClone.UpdateLighting();
					UpdateMapPreview();

					if (directionalPositionConfigAutoToggle != null)
						directionalPositionConfigAutoToggle.SetIsOnWithoutNotify(false);
				},
				onClose: () => { if (null != skyboxTexture) Destroy(skyboxTexture); });
			}
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Orbit / Preview Camera
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

			orbitController.PivotOffset = new Vector3(0, -0.5f, 0);
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
			var cam = ScenePreviewUtil.PreviewCamera;
			if (cam == null || orbitController == null) return;

			var (pos, rot) = orbitController.GetCameraTransform();
			cam.transform.SetPositionAndRotation(pos, rot);
			cam.fieldOfView = fieldOfView;
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Map List & Selection
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

			if (musicDropdown != null)
				musicDropdown.onValueChanged.AddListener(OnMusicDropdownValueChanged);

			if (effectDropdown != null)
				effectDropdown.onValueChanged.AddListener(OnEffectDropdownValueChanged);

			if (directionalPositionConfigAutoToggle != null)
				directionalPositionConfigAutoToggle.onValueChanged.AddListener(isOn => OnDirectionalPositionConfigTogglePressed(directionalPositionConfigAutoToggle));

			if (directionalPositionConfigButton != null)
				directionalPositionConfigButton.onClick.AddListener(() => OnDirectionalPositionConfigButtonPressed(directionalPositionConfigButton));
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

		private void RefreshMapList()
		{
			ClearMapListItems();

			var maps = ResourceManager.Maps;
			if (maps.Count == 0)
			{
				lastSelectedMapIndex = -1;
				InitialiseMapPreview();
				return;
			}

			for (var i = 0; i < maps.Count; i++)
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
				var display = map.name ?? "Unnamed";
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

			InitialiseMapPreview();

			SyncColorTogglesToCurrentMap();
			SyncColorButtonsToCurrentMap();
			SyncSkyboxDropdown();
			SyncCharacterDropdown();
			SyncMusicDropdown();
			SyncEffectDropdown();
		}

		private void UpdateDeleteButtonState()
		{
			if (ButtonDelete == null) return;
			var hasSelection = lastSelectedMapIndex >= 0;
			ButtonDelete.interactable = hasSelection;

			if (ButtonDelete.GetComponentInChildren<TMP_Text>() is TMP_Text txt)
				txt.text = "Delete";
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
			var index = dropdown.options.FindIndex(opt => opt.text.Equals(currentValue, StringComparison.OrdinalIgnoreCase));
			dropdown.SetValueWithoutNotify(index >= 0 ? index : 0);
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Map CRUD Operations
		// ────────────────────────────────────────────────────────────────────────────────

		private void InsertMap()
		{
			var newName = GenerateUniqueMapName("Map");
			var newMap = new Map(16, 16, newName);

			var insertIndex = (lastSelectedMapIndex >= 0)
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

			var idx = lastSelectedMapIndex;
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
			var i = lastSelectedMapIndex;
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
			var i = lastSelectedMapIndex;
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
			var suffix = 1;
			do
			{
				candidate = $"{prefix} {suffix++}";
			} while (existing.Contains(candidate));

			return candidate;
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Preview Management  (THIS IS THE PART THAT WAS LEAKING)
		// ────────────────────────────────────────────────────────────────────────────────

		private bool InitialiseMapPreview()
		{
			if (ScenePreviewUtil.PreviewCamera == null || previewImage == null) return false;

			// CRITICAL: Always clean up previous preview first
			CleanupPreview();

			var map = CurrentMap;
			if (map == null || map.Width <= 0 || map.Height <= 0)
			{
				previewImage.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
				orbitController?.ResetView(false);
				return false;
			}

			previewImage.color = Color.white;

			currentPreviewRoot = new GameObject($"Preview – {map.name ?? "Untitled"}");
			currentPreviewRoot.transform.SetParent(ScenePreviewUtil.PreviewMapRoot ?? transform);

			currentPreviewRoot.transform.localPosition = Vector3.zero;
			currentPreviewRoot.transform.localRotation = Quaternion.identity;

			currentClone = MapUtils.Clone(map, currentPreviewRoot.transform);

			if (null == currentClone)
			{
				previewImage.color = new Color(1f, 0.3f, 0.3f, 0.7f);
				orbitController?.ResetView(false);
				return false;
			}

			var bounds = new Bounds();
			var hasRenderers = false;

			foreach (var rend in currentPreviewRoot.GetComponentsInChildren<Renderer>())
			{
				if (!hasRenderers) { bounds = rend.bounds; hasRenderers = true; }
				else bounds.Encapsulate(rend.bounds);
			}

			if (!hasRenderers)
				bounds = new Bounds(new Vector3(map.Width * 0.5f, 0, map.Height * 0.5f), new Vector3(map.Width, 5f, map.Height));

			ScenePreviewUtil.Update();
			orbitController?.Reframe(bounds, distanceMultiplier: 1f);

			ScenePreviewUtil.UpdateEffect(currentClone.Effect);
			ScenePreviewUtil.UpdateRenderSettings(currentClone.RenderSettings);

			return true;
		}

		private void UpdateMapPreview()
		{
			if (null == currentClone)
				return;

			LeakDetector.LogSnapshot("BEFORE Preview Update");

			// This now properly triggers cleanup inside ScenePreviewUtil / ReflectionEffectCamera
			ScenePreviewUtil.UpdateEffect(currentClone.Effect);
			ScenePreviewUtil.UpdateRenderSettings(currentClone.RenderSettings);

			UpdateMainView();

			LeakDetector.LogSnapshot("AFTER Preview Update");

			void UpdateMainView()
			{
				if (CurrentMap.name == MainController.CurrentMap.name)
				{
					CurrentMap.CopyFrom(currentClone);
				}
				else
				{
					CurrentMap.effect = currentClone.effect;
					CurrentMap.skybox = currentClone.skybox;
					CurrentMap.skyrgb = currentClone.skyrgb;
					CurrentMap.skyvec = currentClone.skyvec;
					CurrentMap.ambient = currentClone.ambient;
				}
			}
		}
	}
}