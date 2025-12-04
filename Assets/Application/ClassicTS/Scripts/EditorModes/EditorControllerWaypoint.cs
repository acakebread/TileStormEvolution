using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class EditorControllerWaypoint : EditorControllerMovement
	{
		public int SelectedWaypointIndex { get; private set; } = -1;

		private int draggingIndex = -1;
		private int originalTile = -1;

		private int pendingAddTile = -1;      // "Add waypoint here?" popup
		private int pendingDeleteIndex = -1;  // "Delete waypoint?" popup

		public EditorControllerWaypoint(EditorController editorController) : base(editorController)
		{
			var ui = editorController.GetEditorUI();
			if (ui != null)
			{
				ui.OnWaypointSelected += SelectWaypoint;
				ui.OnWaypointMoveUp += i => MoveWaypoint(i, -1);
				ui.OnWaypointMoveDown += i => MoveWaypoint(i, 1);
			}
		}

		public override void OnEnable()
		{
			base.OnEnable();
			SelectedWaypointIndex = -1;
			EditorUtil.DestroyWaypointVisuals();
			RebuildMarkers();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			EditorUtil.DestroyWaypointVisuals();
			pendingAddTile = -1;
			pendingDeleteIndex = -1;
		}

		public void OnMapChanged()
		{
			if (editorController.CurrentMode == EditorController.EditorMode.Waypoint)
				RebuildMarkers();
		}

		private void RebuildMarkers()
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			EditorUtil.UpdateWaypointMarkers(
				editorController.iMapManager,
				map.waypoints ?? System.Array.Empty<Waypoint>(),
				SelectedWaypointIndex
			);
		}

		private void SelectWaypoint(int index)
		{
			SelectedWaypointIndex = index;
			RebuildMarkers();
		}

		private void AddWaypointAtTile(int tile)
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			var wp = new Waypoint
			{
				name = $"Waypoint {(map.waypoints?.Length ?? 0)}",
				tile = tile
			};

			var list = map.waypoints?.ToList() ?? new List<Waypoint>();
			list.Add(wp);
			map.waypoints = list.ToArray();

			SelectedWaypointIndex = list.Count - 1;
			RebuildMarkers();
		}

		private void DeleteWaypoint(int index)
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null || map.waypoints == null || index < 0 || index >= map.waypoints.Length)
				return;

			var list = map.waypoints.ToList();
			list.RemoveAt(index);
			map.waypoints = list.ToArray();

			if (SelectedWaypointIndex >= list.Count)
				SelectedWaypointIndex = list.Count - 1;

			RebuildMarkers();
		}

		private void MoveWaypoint(int index, int direction)
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null || map.waypoints == null) return;

			var list = map.waypoints.ToList();
			int newIdx = index + direction;

			if (newIdx < 0 || newIdx >= list.Count) return;

			(list[index], list[newIdx]) = (list[newIdx], list[index]);
			map.waypoints = list.ToArray();
			SelectedWaypointIndex = newIdx;
			RebuildMarkers();
		}

		public override void Update()
		{
			base.Update();

			// Safety guards — prevent null refs
			if (camera == null ||
				editorController?.GetEditorUI() == null ||
				editorController.GetEditorUI().IsGuiControlActive() ||
				EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
				return;

			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			var snapped = editorController.iMapManager.SnappedMapPosition(worldPos);
			int tileUnderMouse = editorController.iMapManager.WorldToMapIndex(snapped);

			// LEFT CLICK
			if (Input.GetMouseButtonDown(0))
			{
				Ray ray = camera.ScreenPointToRay(Input.mousePosition);

				if (Physics.Raycast(ray, out RaycastHit hit))
				{
					if (hit.collider != null && hit.collider.gameObject.name.StartsWith("WP"))
					{
						if (int.TryParse(hit.collider.gameObject.name.Substring(2), out int index))
						{
							SelectWaypoint(index);
							draggingIndex = index;
							originalTile = editorController.iMapManager.CurrentMap.waypoints[index].tile;
							pendingAddTile = -1;
							pendingDeleteIndex = -1;
							return;
						}
					}
				}

				// Clicked empty tile → show add popup
				if (tileUnderMouse >= 0)
				{
					pendingAddTile = tileUnderMouse;
				}
			}

			// RIGHT CLICK → show delete popup
			if (Input.GetMouseButtonDown(1))
			{
				Ray ray = camera.ScreenPointToRay(Input.mousePosition);
				if (Physics.Raycast(ray, out RaycastHit hit))
				{
					if (hit.collider != null && hit.collider.gameObject.name.StartsWith("WP"))
					{
						if (int.TryParse(hit.collider.gameObject.name.Substring(2), out int index))
						{
							SelectWaypoint(index);
							pendingDeleteIndex = index;
						}
					}
				}
			}

			// DRAG
			if (draggingIndex >= 0 && Input.GetMouseButton(0) && tileUnderMouse >= 0)
			{
				var map = editorController.iMapManager.CurrentMap;
				if (map?.waypoints != null && draggingIndex < map.waypoints.Length)
				{
					map.waypoints[draggingIndex].tile = tileUnderMouse;
					RebuildMarkers();
				}
			}

			// DROP
			if (draggingIndex >= 0 && Input.GetMouseButtonUp(0))
			{
				if (tileUnderMouse < 0)
				{
					var map = editorController.iMapManager.CurrentMap;
					if (map?.waypoints != null && draggingIndex < map.waypoints.Length)
					{
						map.waypoints[draggingIndex].tile = originalTile;
						RebuildMarkers();
					}
				}
				draggingIndex = -1;
				originalTile = -1;
			}
		}

		public override void OnGui()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Waypoint || camera == null) return;

			// === DELETE POPUP ===
			if (pendingDeleteIndex >= 0)
			{
				var map = editorController.iMapManager?.CurrentMap;
				if (map == null || map.waypoints == null || pendingDeleteIndex >= map.waypoints.Length)
				{
					pendingDeleteIndex = -1;
				}
				else
				{
					var wp = map.waypoints[pendingDeleteIndex];
					Vector3 worldPos = editorController.iMapManager.TileWorldPosition(wp.tile) + Vector3.up * 0.8f;
					Vector3 screenPos = camera.WorldToScreenPoint(worldPos);

					if (screenPos.z > 0)
					{
						screenPos.y = Screen.height - screenPos.y;
						Rect r = new Rect(screenPos.x - 110, screenPos.y - 50, 220, 100);

						GUI.Box(r, "", GUI.skin.window);
						GUILayout.BeginArea(r);
						GUILayout.BeginVertical();

						GUILayout.Space(12);
						GUI.color = new Color(1f, 0.3f, 0.3f);
						GUILayout.Label("Delete waypoint?", EditorStyles.boldLabel);
						GUI.color = Color.white;
						GUILayout.Label($"WP{pendingDeleteIndex:00} at tile {wp.tile}");

						GUILayout.Space(8);
						GUILayout.BeginHorizontal();
						GUILayout.FlexibleSpace();
						if (GUILayout.Button("Delete", GUILayout.Width(90)))
						{
							DeleteWaypoint(pendingDeleteIndex);
							pendingDeleteIndex = -1;
						}
						if (GUILayout.Button("Cancel", GUILayout.Width(90)))
						{
							pendingDeleteIndex = -1;
						}
						GUILayout.FlexibleSpace();
						GUILayout.EndHorizontal();
						GUILayout.Space(8);

						GUILayout.EndVertical();
						GUILayout.EndArea();

						if (Input.GetMouseButtonDown(0))
						{
							Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
							if (!r.Contains(m))
								pendingDeleteIndex = -1;
						}
					}
					else
					{
						pendingDeleteIndex = -1;
					}
				}
			}

			// === ADD POPUP ===
			if (pendingAddTile >= 0)
			{
				Vector3 worldPos = editorController.iMapManager.TileWorldPosition(pendingAddTile) + Vector3.up * 0.6f;
				Vector3 screenPos = camera.WorldToScreenPoint(worldPos);

				if (screenPos.z > 0)
				{
					screenPos.y = Screen.height - screenPos.y;
					Rect r = new Rect(screenPos.x - 100, screenPos.y - 40, 200, 80);

					GUI.Box(r, "", GUI.skin.window);
					GUILayout.BeginArea(r);
					GUILayout.BeginVertical();
					GUILayout.Space(10);
					GUILayout.Label("Add waypoint here?", EditorStyles.boldLabel);

					GUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();
					if (GUILayout.Button("Add", GUILayout.Width(80)))
					{
						AddWaypointAtTile(pendingAddTile);
						pendingAddTile = -1;
					}
					if (GUILayout.Button("Cancel", GUILayout.Width(80)))
					{
						pendingAddTile = -1;
					}
					GUILayout.FlexibleSpace();
					GUILayout.EndHorizontal();
					GUILayout.Space(10);
					GUILayout.EndVertical();
					GUILayout.EndArea();

					if (Input.GetMouseButtonDown(0))
					{
						Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
						if (!r.Contains(m))
							pendingAddTile = -1;
					}
				}
				else
				{
					pendingAddTile = -1;
				}
			}
		}
	}
}