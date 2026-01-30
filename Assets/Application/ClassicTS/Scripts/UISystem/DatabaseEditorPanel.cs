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

		#endregion

		// Runtime state
		private readonly List<Toggle> spawnedMapToggles = new List<Toggle>();
		private ToggleGroup toggleGroup;

		private static int lastSelectedMapIndex = 0;

		private Map CurrentMap =>
			lastSelectedMapIndex >= 0 && lastSelectedMapIndex < ResourceManager.Maps.Count
				? ResourceManager.Maps[lastSelectedMapIndex]
				: null;

		protected override void Awake()
		{
			base.Awake();
			InitializeUIReferences();
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			RefreshMapList();

			MapPreviewUtil.Initialize(CurrentMap);
			MapPreviewUtil.SetPreviewLayer(LayerMask.NameToLayer(MapPreviewUtil.PREVIEW_LAYER_NAME));

			if (previewImage != null)
			{
				previewImage.texture = MapPreviewUtil.PreviewRenderTexture;
				previewImage.color = Color.white;           // make sure it's visible
			}

			// Initial preview update
			UpdateMapPreview();

			PopulateSkyboxDropdown();
			SyncSkyboxDropdown();
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

		private void LateUpdate()
		{
			if (gameObject.activeInHierarchy && CurrentMap != null)
			{
				// Re-render every frame to animate the orbit
				// (only if map is selected and preview is active)

				var map = CurrentMap;
				float diag = Mathf.Sqrt(map.Width * map.Width + map.Height * map.Height);
				float fov = 60f;// Mathf.Clamp(15f + diag * 1.6f, 40f, 75f);

				Vector3 center = new Vector3(map.Width * 0.5f, 0f, map.Height * 0.5f);
				float distance = diag * 0.5f + 2f;

				MapPreviewUtil.RenderPreview(center, distance, fov);
			}
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

		// ── Map List Population ─────────────────────────────────────────────────────────────

		private void RefreshMapList()
		{
			ClearMapListItems();

			var maps = ResourceManager.Maps;
			if (maps.Count == 0)
			{
				lastSelectedMapIndex = -1;
				UpdateMapPreview();     // clears preview
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

			if (null != map.skybox) SetSkybox(map.skybox);

			SyncSkyboxDropdown();
			UpdateMapPreview();
		}

		private void UpdateDeleteButtonState()
		{
			if (ButtonDelete == null) return;

			bool hasSelection = lastSelectedMapIndex >= 0;
			bool cannotDelete = false; // ← add real logic later if needed

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

		// ── Preview Logic ────────────────────────────────────────────────────────────────

		private GameObject currentPreviewInstance;

		private void UpdateMapPreview()
		{
			if (MapPreviewUtil.PreviewCamera == null || previewImage == null)
				return;

			var map = CurrentMap;

			// Clear previous
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
				MapPreviewUtil.previewMapRoot,
				MapPreviewUtil.previewLayer
			);

			if (currentPreviewInstance == null)
			{
				previewImage.color = new Color(1f, 0.3f, 0.3f, 0.7f);
				return;
			}

			// ── Camera orbiting setup ────────────────────────────────────────
			float diag = Mathf.Sqrt(map.Width * map.Width + map.Height * map.Height);
			float fov = 60f;// Mathf.Clamp(35f + diag * 1.6f, 40f, 75f);

			// Center of the map
			Vector3 center = new Vector3(
				map.Width * 0.5f,
				0f,
				map.Height * 0.5f
			);

			// Distance — your current formula, but slightly farther for rotation
			float distance = diag * 0.5f + 2f; // tweak multiplier/offset to taste

			// Render with orbiting
			MapPreviewUtil.RenderPreview(center, distance, fov);
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
			//SkyboxUtility.SetSkybox(value);//temporary workaround to deal with the issue that the preview cannot have different skybox to global skybox

			var skyMaterial = SkyboxUtility.GetSkyboxMaterialForName(value);
			 if (null != skyMaterial)
				MapPreviewUtil.SetSkyboxOverride(skyMaterial);

			//var mainReflection = Camera.main?.GetComponent<ReflectionEffectCamera>();
			//if (mainReflection != null)
			//{
			//	var mainSkyMat = SkyboxUtility.GetSkyboxMaterialForName(value);
			//	if (mainSkyMat != null)
			//		mainReflection.SetSkyboxOverride(mainSkyMat);
			//}
		}
	}
}
