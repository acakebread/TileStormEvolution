using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public class DatabaseEditorPanel : UIPanel
	{
		#region Serialized Fields - UI References

		[Header("UI References")]
		[SerializeField] private Button closeButton;
		[SerializeField] private ScrollRect mapScrollView;
		[SerializeField] private Transform contentParent;
		[SerializeField] private GameObject mapListItemPrefab;           // ← assign same or similar prefab as definitionListItemPrefab

		[SerializeField] private Button ButtonInsert;
		[SerializeField] private Button ButtonDelete;
		[SerializeField] private Button ButtonMoveUp;
		[SerializeField] private Button ButtonMoveDown;

		// Add your own later: name input, size label, preview minimap, etc.
		//[SerializeField] private TMP_InputField mapNameInput;
		//[SerializeField] private TextMeshProUGUI mapSizeLabel;

		//[Header("Preview")]
		//[SerializeField] private RawImage previewImage;

		//[Header("Dynamic Properties Panel")]
		//[SerializeField] private RectTransform flagPropertiesRect;
		//[SerializeField] private GameObject flagTogglePrefab;

		//[Header("ID Input")]
		//[SerializeField] private TMP_InputField IDInput;

		//[Header("Dropdowns")]
		//[SerializeField] private TMP_Dropdown modelDropdown;
		//[SerializeField] private string noneModelOptionText = "— None —";

		//[SerializeField] private TMP_Dropdown textureSequenceDropdown;
		//[SerializeField] private string noneTextureOptionText = "— None —";

		//[SerializeField] private TMP_Dropdown materialDropdown;
		//[SerializeField] private string noneMaterialOptionText = "— None —";

		#endregion

		#region Serialized Fields - Preview Settings

		//[Header("Preview Settings")]
		//[SerializeField] private Color backgroundColor = new Color(0.129f, 0.698f, 0.882f);
		//[SerializeField] private float fieldOfView = 60f;
		//[SerializeField] private float sizeToDistanceFactor = 1f;
		//[SerializeField] private float defaultTiltAngle = 30f;
		//[SerializeField] private float minTiltAngle = 0f;
		//[SerializeField] private float maxTiltAngle = 90f;
		//[SerializeField] private float minDistance = 0.8f;
		//[SerializeField] private float maxDistance = 10f;
		//[SerializeField] private float dragOrbitSensitivity = 0.2f;
		//[SerializeField] private float dragTiltSensitivity = 0.2f;
		//[SerializeField] private float scrollZoomSensitivity = 0.5f;
		//[SerializeField] private float autoRotateSpeed = -15f;

		//[Header("Ground Plane Settings")]
		//[SerializeField] private Color groundColor = Color.white;
		//[SerializeField] private float groundSize = 2.5f;
		//[SerializeField] private float groundY = -0.01f;
		//[SerializeField] private float groundUVScale = 1f;
		//[SerializeField] private Texture2D groundOverrideTexture;

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
			InitializeButtons();
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			RefreshMapList();
			// Later: PopulateNameField(), UpdatePreview(), etc.
		}

		protected override void OnDisable()
		{
			ClearMapListItems();
			base.OnDisable();
		}

		private void InitializeUIReferences()
		{
			if (closeButton != null)
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));

			if (contentParent == null && mapScrollView != null)
				contentParent = mapScrollView.content;

			toggleGroup = contentParent.GetComponent<ToggleGroup>()
				?? contentParent.gameObject.AddComponent<ToggleGroup>();
			toggleGroup.allowSwitchOff = false;
		}

		private void InitializeButtons()
		{
			if (ButtonInsert) ButtonInsert.onClick.AddListener(InsertMap);
			if (ButtonDelete) ButtonDelete.onClick.AddListener(DeleteMap);
			if (ButtonMoveUp) ButtonMoveUp.onClick.AddListener(MoveMapUp);
			if (ButtonMoveDown) ButtonMoveDown.onClick.AddListener(MoveMapDown);
		}

		// ── Map List Population ─────────────────────────────────────────────────────────────

		private void RefreshMapList()
		{
			ClearMapListItems();

			var maps = ResourceManager.Maps;
			if (maps.Count == 0)
			{
				lastSelectedMapIndex = -1;
				// Clear any right-side fields here later
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
				// Customize display — feel free to change format
				string display = map.name ?? "Unnamed";

				// Optional extra info
				if (map.width > 0 && map.height > 0)
					display += $"  ({map.width}×{map.height})";

				// Optional: show object count, spawn point presence, etc.
				// int objCount = map.layers?.Sum(l => l?.Count ?? 0) ?? 0;
				// display += $"  [{objCount} objs]";

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

			// Later: fill name field, size label, minimap preview, etc.
			//if (mapNameInput != null) mapNameInput.text = map?.name ?? "";
			//if (mapSizeLabel != null)  mapSizeLabel.text  = map != null ? $"{map.width} × {map.height}" : "—";

			UpdateDeleteButtonState();

			if (index >= 0 && index < spawnedMapToggles.Count)
				spawnedMapToggles[index].SetIsOnWithoutNotify(true);
		}

		private void UpdateDeleteButtonState()
		{
			if (ButtonDelete == null) return;

			bool hasSelection = lastSelectedMapIndex >= 0;
			// Later: check if map is used / protected / current play map / etc.
			bool cannotDelete = false; // placeholder

			ButtonDelete.interactable = hasSelection && !cannotDelete;

			if (ButtonDelete.GetComponentInChildren<TMP_Text>() is TMP_Text txt)
			{
				txt.text = cannotDelete ? "Delete (locked)" : "Delete";
			}
		}

		// ── CRUD Operations ──────────────────────────────────────────────────────────────

		private void InsertMap()
		{
			string newName = GenerateUniqueMapName("Map");

			// Use the new constructor/factory
			var newMap = new Map(16, 16, newName);
			// or: var newMap = Map.CreateEmpty(16, 16, newName);

			int insertIndex = (lastSelectedMapIndex >= 0)
				? lastSelectedMapIndex + 1
				: ResourceManager.Maps.Count;

			var list = ResourceManager.Maps.ToList();
			list.Insert(insertIndex, newMap);

			// Write back to database
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
	}
}