using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

		[Header("Preview Orbit & Camera")]
		// ── Camera orbit control fields (unchanged, but no lerp anymore) ────────────────────────
		[Header("Preview Orbit & Camera")]
		[SerializeField] private float orbitSpeed = 18f;
		[SerializeField] private float baseTiltAngle = 25f;
		[SerializeField] private float extraTiltAngle = 10f;
		[SerializeField] private float distanceMultiplier = 0.35f;
		[SerializeField] private float distanceOffset = 0f;//3.5f;
		[SerializeField] private float minDistance = 5f;
		[SerializeField] private float maxDistance = 80f;
		[SerializeField] private float defaultFOV = 60f;

		private float currentOrbitAngle = 45f;
		private Vector3 currentMapCenter = Vector3.zero;
		private float currentDistance = 12f;
		private float currentFOV;

		#endregion

		// Runtime state
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
		}

		protected override void OnDisable()
		{
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

			base.OnDisable();
		}

		private void Update()
		{
			if (!isActiveAndEnabled || CurrentMap == null)
				return;

			MapPreviewUtil.UpdateRenderTextureSizeIfNeeded();

			// Increment orbit (smooth rotation over time)
			currentOrbitAngle += orbitSpeed * Time.deltaTime;
			currentOrbitAngle %= 360f;

			UpdatePreviewCamera();
		}

		// ── Preview Logic ────────────────────────────────────────────────────────────────

		private void UpdatePreviewCamera()
		{
			var cam = MapPreviewUtil.PreviewCamera;
			if (cam == null) return;

			var map = CurrentMap;
			if (map == null) return;

			// Recompute center + distance every frame (cheap, and ensures correctness after map change)
			currentMapCenter = new Vector3(map.Width * 0.5f, 0f, map.Height * 0.5f);

			float diag = Mathf.Sqrt(map.Width * map.Width + map.Height * map.Height);
			float targetDistance = diag * distanceMultiplier + distanceOffset;
			currentDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

			// Apply position/rotation/FOV
			currentFOV = defaultFOV; // can become dynamic later if needed

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

			// Reset orbit view on map change
			currentFOV = defaultFOV;

			UpdatePreviewCamera();
		}

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

			// Only update + react if actually changed
			if (newCharacter != CurrentMap.character)
			{
				CurrentMap.character = newCharacter;
				// TODO: later — apply character preview / reload model / etc.
				// For now it just saves the selection
			}
		}

		// ── Map List Population ─────────────────────────────────────────────────────────────

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
				// ↑ Add more here later when real resources are ready
			};

			PopulateDropdown(characterDropdown, characterNames, noneCharacterOptionText);
		}

		private void SyncCharacterDropdown()
		{
			if (CurrentMap == null) return;

			// Assuming Map class will eventually get a field like: public string Character { get; set; }
			SyncDropdown(characterDropdown, CurrentMap.character, noneCharacterOptionText);
		}

		// ── CRUD Operations ──────────────────────────────────────────────────────────────

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

		// ── Helpers ─────────────────────────────────────────────────────────────────────

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
	}
}


      //"Eggbot Default",
      //"Eggbot Industrial",
      //"Eggbot Egypt",
      //"Eggbot Medieval",
      //"Eggbot Jungle",
