using UnityEngine;
using System.Linq;
using MassiveHadronLtd;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour
	{
		private MapManager mapManager;
		private GameObject gridLines;
		private EditorControllerMovement activeMode;
		private EditorControllerDrag dragMode;
		private EditorControllerPaint paintMode;
		public enum EditorMode { Drag, Paint }
		private Vector3 mouseDownPos; // Mouse position on RMB down for delete
		private Vector3 mouseDownPosLMB; // Mouse position on LMB down for placement
		private int mouseDownMapIndex = -1; // Map index on LMB down
		private List<int> tileDefCycleList; // List of TileDef indices for cycling
		private int cycleIndex = 0; // Current position in cycle list
		private int selectedMapDefIndex = 0; // Index into mapDefs

		//temporary
		private PlaceholderUI placeholderUI; // Reference to PlaceholderUI
		private PlaceholderEditorUI editorUI; // Reference to PlaceholderEditorUI

		private void Awake()
		{
			if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

			placeholderUI = FindAnyObjectByType<PlaceholderUI>();
			editorUI = FindAnyObjectByType<PlaceholderEditorUI>();
			if (null == placeholderUI) Debug.LogWarning("PlaceholderUI not found in scene!");
			if (null == editorUI) Debug.LogWarning("PlaceholderEditorUI not found in scene!");

			// Initialize ghost material
			GeometryUtil.InitializeGhostMaterial();
		}

		public void Initialise(MapManager map)
		{
			Destroy();

			// Set default system
			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			controller.SetCameraSystem(CameraModeRegistry.Editor, true);

			mapManager = map;

			// Initialize PlaceholderEditorUI
			editorUI = FindAnyObjectByType<PlaceholderEditorUI>();
			if (null == editorUI)
			{
				Debug.LogError("PlaceholderEditorUI not found in scene!");
				return;
			}
			var cameraSystem = controller.currentCamera;
			var camera = cameraSystem.camera;
			editorUI.Initialize(this, mapManager, camera);

			gridLines = null != mapManager ? GridLinesHelper.CreateGridLines(transform, mapManager.Width, mapManager.Height) : null;
			UpdateGridLines(editorUI.GetGridLinesEnabled());

			if (isActiveAndEnabled) OnEnable();
		}

		public void UpdateGridLines(bool value)
		{
			if (null != gridLines) gridLines.SetActive(value);
			editorUI.SetGridLinesEnabled(value);
		}

		private void Update()
		{
			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			var cameraSystem = controller.currentCamera;
			var camera = cameraSystem.camera;

			// Workaround: Reset hotControl on mouse release to handle drag outside GUI
			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
				GUIUtility.hotControl = 0;

			if (activeMode != null)
				activeMode.Update();

			// Update ghost tile position and handle delete
			if (editorUI.currentMode == EditorMode.Paint && !editorUI.IsMouseOverGui() && !EventSystem.current.IsPointerOverGameObject())
			{
				var tempSelectedTileDefGlobalIndex = editorUI.GetSelectedTileDefGlobalIndex();
				if (tempSelectedTileDefGlobalIndex >= 0 && tempSelectedTileDefGlobalIndex < DatabaseSerializer.TileDefs.Count)
				{
					var tileDef = DatabaseSerializer.TileDefs[tempSelectedTileDefGlobalIndex];
					GeometryUtil.UpdateGhostTile(camera, mapManager, tileDef);

					// Track mouse down for delete (RMB)
					if (Input.GetMouseButtonDown(1))
						mouseDownPos = Input.mousePosition;

					// Handle click-and-release for delete (RMB)
					if (Input.GetMouseButtonUp(1))
					{
						var mouseMoveDistance = Vector3.Distance(Input.mousePosition, mouseDownPos);
						if (mouseMoveDistance < 5f) // Threshold: 5 pixels
						{
							var emptyTileDefIndex = mapManager.GetOrAddMapDefIndex("tile_empty", "Default");
							if (emptyTileDefIndex >= 0)
							{
								paintMode.SetTileDefIndex(emptyTileDefIndex);
								paintMode.PlaceTileAtMousePosition();
							}
						}
					}
				}
				else
				{
					GeometryUtil.HideGhostTile();
				}
			}
			else
			{
				GeometryUtil.HideGhostTile();
			}

			// Handle tile placement and cycling
			if (editorUI.currentMode == EditorMode.Paint && !editorUI.IsMouseOverGui() && !EventSystem.current.IsPointerOverGameObject())
			{
				// Track mouse down for LMB to verify same grid cell
				if (Input.GetMouseButtonDown(0))
				{
					mouseDownPosLMB = Input.mousePosition;
					var ray = camera.ScreenPointToRay(mouseDownPosLMB);
					var plane = new Plane(Vector3.up, Vector3.zero);
					if (plane.Raycast(ray, out float enter))
					{
						Vector3 worldPos = ray.GetPoint(enter);
						mouseDownMapIndex = mapManager.WorldToMapIndex(worldPos);
					}
					else
					{
						mouseDownMapIndex = -1;
					}
				}

				// Handle tile placement and cycling on mouse up (LMB)
				if (Input.GetMouseButtonUp(0))
				{
					var ray = camera.ScreenPointToRay(Input.mousePosition);
					var plane = new Plane(Vector3.up, Vector3.zero);
					if (plane.Raycast(ray, out float enter))
					{
						var worldPos = ray.GetPoint(enter);
						var mapIndex = mapManager.WorldToMapIndex(worldPos);
						if (mapIndex >= 0 && mapIndex < mapManager.Count && mapIndex == mouseDownMapIndex)
						{
							var tempSelectedTileDefGlobalIndex = editorUI.GetSelectedTileDefGlobalIndex();
							var selectedTileDef = DatabaseSerializer.TileDefs[tempSelectedTileDefGlobalIndex];
							var selectedTileDefIndex = mapManager.GetOrAddMapDefIndex(selectedTileDef.szType, selectedTileDef.szTheme);

							// Get the current tile's MapTileDef index at the clicked position
							var currentMapDefIndex = mapManager.GetTileDefIndexAt(mapIndex);
							var tilesMatch = false;
							var mapDefs = mapManager.GetMapDefs();
							if (currentMapDefIndex >= 0 && currentMapDefIndex < mapDefs.Length)
							{
								var currentTileDef = mapDefs[currentMapDefIndex];
								tilesMatch = currentTileDef.szType == selectedTileDef.szType && currentTileDef.szTheme == selectedTileDef.szTheme;
							}

							if (tilesMatch)
							{
								// Same tile type, cycle to the next in the group
								if (tileDefCycleList != null && tileDefCycleList.Count > 1)
								{
									cycleIndex = (cycleIndex + 1) % tileDefCycleList.Count;
									tempSelectedTileDefGlobalIndex = tileDefCycleList[cycleIndex];
									editorUI.SetSelectedTileDefGlobalIndex(tempSelectedTileDefGlobalIndex);
									var newTileDef = DatabaseSerializer.TileDefs[tempSelectedTileDefGlobalIndex];
									selectedMapDefIndex = mapManager.GetOrAddMapDefIndex(newTileDef.szType, newTileDef.szTheme);
									paintMode.SetTileDefIndex(selectedMapDefIndex);
									GeometryUtil.DestroyGhostTile();
									GeometryUtil.UpdateGhostTile(camera, mapManager, newTileDef);
									paintMode.PlaceTileAtMousePosition();
								}
							}
							else
							{
								// Different tile type, place the selected tile
								paintMode.SetTileDefIndex(selectedTileDefIndex);
								paintMode.PlaceTileAtMousePosition();
							}
						}
					}
					mouseDownMapIndex = -1; // Reset after mouse up
				}
			}
		}

		private void UpdateTileCycleList(string currentTileType)
		{
			// Define suffix groups
			var singleDirections = new[] { " n", " e", " s", " w" };
			var doubleLinear = new[] { " ew", " ns", " we", " sn" };
			var doubleDiagonal = new[] { " ne", " nw", " se", " sw" };
			string[] selectedGroup = null;

			// Determine the base tile type by removing the suffix from currentTileType
			var derivedBaseTileType = currentTileType;
			foreach (var suffix in singleDirections.Concat(doubleLinear).Concat(doubleDiagonal))
			{
				if (currentTileType.EndsWith(suffix))
				{
					derivedBaseTileType = currentTileType.Substring(0, currentTileType.Length - suffix.Length);
					break;
				}
			}

			// Determine the group based on the current tile's suffix
			if (singleDirections.Any(suffix => currentTileType.EndsWith(suffix)))
				selectedGroup = singleDirections;
			else if (doubleLinear.Any(suffix => currentTileType.EndsWith(suffix)))
				selectedGroup = doubleLinear;
			else if (doubleDiagonal.Any(suffix => currentTileType.EndsWith(suffix)))
				selectedGroup = doubleDiagonal;
			else
				selectedGroup = singleDirections; // Fallback to single directions if no suffix or base tile

			tileDefCycleList = new List<int>();

			// Include base tile if it exists
			for (var i = 0; i < DatabaseSerializer.TileDefs.Count; i++)
			{
				if (DatabaseSerializer.TileDefs[i].szType == derivedBaseTileType)
				{
					tileDefCycleList.Add(i);
					break;
				}
			}

			// Add tiles with suffixes from the selected group
			foreach (var suffix in selectedGroup)
			{
				for (var i = 0; i < DatabaseSerializer.TileDefs.Count; i++)
				{
					if (DatabaseSerializer.TileDefs[i].szType == derivedBaseTileType + suffix)
					{
						tileDefCycleList.Add(i);
						break;
					}
				}
			}
		}

		void OnEnable()
		{
			UpdateGridLines(editorUI.GetGridLinesEnabled());

			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			var cameraSystem = controller.currentCamera;
			var camera = cameraSystem.camera;
			camera.transform.position = cameraSystem.iorigin;
			var direction = cameraSystem.itarget - cameraSystem.iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

			dragMode = new EditorControllerDrag(camera);
			paintMode = new EditorControllerPaint(camera, mapManager, selectedMapDefIndex);
			activeMode = dragMode;

			SetMode(editorUI.currentMode);
		}

		void OnDisable()
		{
			UpdateGridLines(false);
		}

		void Destroy()
		{
			if (null != gridLines) Destroy(gridLines);
		}

		void OnDestroy()
		{
			Destroy();
			GeometryUtil.DestroyGhostTile();
		}

		public void OnApplicationFocus(bool hasFocus)
		{
			if (null != activeMode) activeMode.OnApplicationFocus(hasFocus);
		}

		public void SetMode(EditorMode mode)
		{
			activeMode = mode == EditorMode.Drag ? dragMode : paintMode;
		}

		public void SetSelectedTileDef(int mapDefIndex, int globalIndex)
		{
			selectedMapDefIndex = mapDefIndex;
			paintMode.SetTileDefIndex(mapDefIndex);
			UpdateTileCycleList(DatabaseSerializer.TileDefs[globalIndex].szType);
			cycleIndex = tileDefCycleList.IndexOf(globalIndex);
			if (cycleIndex < 0) cycleIndex = 0;
		}
	}
}