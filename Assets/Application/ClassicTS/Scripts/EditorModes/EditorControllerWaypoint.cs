using MassiveHadronLtd;
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
		private int pendingAddTile = -1;
		private int pendingDeleteIndex = -1;
		private Vector2 scrollPos = Vector2.zero;

		// Our own animated panel — uses your existing GuiUtils.AutoHidePanel perfectly
		private readonly GuiUtils.AutoHidePanel sidePanel = new(
			collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f);

		public override bool IsMouseOverModeGui()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Waypoint)
				return false;

			float panelWidth = sidePanel.CurrentWidth;
			var screenRect = new Rect(Screen.width - panelWidth - 20f, 20f, panelWidth, Screen.height - 40f);

			Vector2 mouse = Input.mousePosition;
			mouse.y = Screen.height - mouse.y;
			return screenRect.Contains(mouse);
		}

		public EditorControllerWaypoint(EditorController editorController) : base(editorController) { }

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

			var wp = new Waypoint { name = $"Waypoint {map.waypoints?.Length ?? 0}", tile = tile };
			var list = map.waypoints?.ToList() ?? new List<Waypoint>();
			list.Add(wp);
			map.waypoints = list.ToArray();

			SelectedWaypointIndex = list.Count - 1;
			RebuildMarkers();
		}

		private void DeleteWaypoint(int index)
		{
			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null || map.waypoints == null || index < 0 || index >= map.waypoints.Length) return;

			var list = map.waypoints.ToList();
			list.RemoveAt(index);
			map.waypoints = list.ToArray();

			if (SelectedWaypointIndex >= list.Count)
				SelectedWaypointIndex = list.Count - 1;

			RebuildMarkers();
		}

		public override void Update()
		{
			base.Update();

			if (camera == null ||
				editorController.IsGuiControlActive() ||
				(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
				return;

			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			var snapped = editorController.iMapManager.SnappedMapPosition(worldPos);
			int tileUnderMouse = editorController.iMapManager.WorldToMapIndex(snapped);

			// Left click
			if (Input.GetMouseButtonDown(0))
			{
				Ray ray = camera.ScreenPointToRay(Input.mousePosition);
				if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider?.name.StartsWith("WP") == true)
				{
					if (int.TryParse(hit.collider.name.Substring(2), out int index))
					{
						SelectWaypoint(index);
						draggingIndex = index;
						originalTile = editorController.iMapManager.CurrentMap.waypoints[index].tile;
						pendingAddTile = pendingDeleteIndex = -1;
						return;
					}
				}

				if (tileUnderMouse >= 0)
					pendingAddTile = tileUnderMouse;
			}

			// Right click
			if (Input.GetMouseButtonDown(1))
			{
				Ray ray = camera.ScreenPointToRay(Input.mousePosition);
				if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider?.name.StartsWith("WP") == true)
				{
					if (int.TryParse(hit.collider.name.Substring(2), out int index))
					{
						SelectWaypoint(index);
						pendingDeleteIndex = index;
					}
				}
			}

			// Drag on map
			if (draggingIndex >= 0 && Input.GetMouseButton(0) && tileUnderMouse >= 0)
			{
				var map = editorController.iMapManager.CurrentMap;
				if (map?.waypoints != null && draggingIndex < map.waypoints.Length)
				{
					map.waypoints[draggingIndex].tile = tileUnderMouse;
					RebuildMarkers();
				}
			}

			if (draggingIndex >= 0 && Input.GetMouseButtonUp(0))
			{
				if (tileUnderMouse < 0)
				{
					var map = editorController.iMapManager.CurrentMap;
					if (map?.waypoints != null && draggingIndex < map.waypoints.Length)
						map.waypoints[draggingIndex].tile = originalTile;
					RebuildMarkers();
				}
				draggingIndex = -1;
				originalTile = -1;
			}
		}

		public override void OnGui()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Waypoint || camera == null) return;

			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;
			Waypoint[] waypoints = map.waypoints ?? System.Array.Empty<Waypoint>();

			// Update side panel animation
			sidePanel.Update();

			Rect panel = sidePanel.GetRect(20f, 20f);

			GUI.backgroundColor = new Color(0.15f, 0.3f, 0.42f, 0.95f);
			GUI.Box(panel, "");
			GUI.backgroundColor = Color.white;

			GUILayout.BeginArea(panel);
			GUILayout.BeginVertical();

			GUILayout.Label("Waypoints", EditorStyles.boldLabel);

			if (waypoints.Length == 0)
			{
				GUILayout.FlexibleSpace();
				GUILayout.Label("No waypoints\nLeft-click map to add",
					new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
				GUILayout.FlexibleSpace();
			}
			else
			{
				// ---------- Scrollable list (manual, layout-free) ----------
				// Reserve space at the bottom for the Move Up / Move Down buttons + tip
				float reservedBottom = 110f; // same as your earlier layout: panel.height - 110
				float topOffset = 25f;       // offset to leave space for the "Waypoints" label
				float scrollHeight = Mathf.Max(0f, panel.height - reservedBottom);

				// Scroll rect is relative to the BeginArea origin (0,0)
				Rect scrollRect = new Rect(0f, topOffset, panel.width, scrollHeight);

				// compute content height explicitly
				int count = waypoints.Length;
				float buttonHeight = 36f;
				float spacing = 4f;
				float contentHeight = count * (buttonHeight + spacing);

				// shrink content width slightly to avoid rounding overflow
				float scrollBarWidth = 12f;
				float epsilon = 10f;
				Rect contentRect = new Rect(0f, 0f, Mathf.Max(1f, scrollRect.width - scrollBarWidth - epsilon), contentHeight);

				// Begin manual scroll view (no horizontal scrollbar)
				scrollPos = GUI.BeginScrollView(scrollRect, scrollPos, contentRect, false, true);

				float y = 0f;
				for (int i = 0; i < waypoints.Length; i++)
				{
					var wp = waypoints[i];
					string cam = wp.IsCamera() ? " [Cam]" : "";
					string label = $"WP{i:00}{cam} [{wp.tile}]";

					// full-width clickable button inside the scroll content
					GUI.backgroundColor = (i == SelectedWaypointIndex) ? new Color(0.3f, 0.8f, 1f, 0.9f) : Color.white;
					if (GUI.Button(new Rect(0f, y, contentRect.width, buttonHeight), label))
					{
						SelectWaypoint(i);
					}
					GUI.backgroundColor = Color.white;

					y += buttonHeight + spacing;
				}

				GUI.EndScrollView();

				// ---------- Move Up / Move Down buttons (outside the scroll view) ----------
				Rect buttonsArea = new Rect(0f, scrollRect.y + scrollRect.height + 6f, panel.width, reservedBottom);
				GUILayout.BeginArea(buttonsArea);
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();

				GUI.enabled = SelectedWaypointIndex > 0;
				if (GUILayout.Button("Move Up", GUILayout.Width(100), GUILayout.Height(30)))
				{
					if (SelectedWaypointIndex > 0)
					{
						var list = waypoints.ToList();
						var temp = list[SelectedWaypointIndex];
						list[SelectedWaypointIndex] = list[SelectedWaypointIndex - 1];
						list[SelectedWaypointIndex - 1] = temp;
						map.waypoints = list.ToArray();
						SelectedWaypointIndex--;
						RebuildMarkers();
					}
				}

				GUI.enabled = SelectedWaypointIndex >= 0 && SelectedWaypointIndex < waypoints.Length - 1;
				if (GUILayout.Button("Move Down", GUILayout.Width(100), GUILayout.Height(30)))
				{
					if (SelectedWaypointIndex >= 0 && SelectedWaypointIndex < waypoints.Length - 1)
					{
						var list = waypoints.ToList();
						var temp = list[SelectedWaypointIndex];
						list[SelectedWaypointIndex] = list[SelectedWaypointIndex + 1];
						list[SelectedWaypointIndex + 1] = temp;
						map.waypoints = list.ToArray();
						SelectedWaypointIndex++;
						RebuildMarkers();
					}
				}

				GUI.enabled = true;
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				// Tip text below buttons
				GUILayout.Space(4);
				GUILayout.Label("Tip: Left-click map to add • Right-click waypoint to delete",
					new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter });

				GUILayout.EndArea();
			}

			GUILayout.EndVertical();
			GUILayout.EndArea();

			// ——— ADD CONFIRMATION POPUP ———
			if (pendingAddTile >= 0)
			{
				Vector3 pos = editorController.iMapManager.TileWorldPosition(pendingAddTile) + Vector3.up * 0.6f;
				Vector3 sp = camera.WorldToScreenPoint(pos);
				if (sp.z > 0)
				{
					sp.y = Screen.height - sp.y;
					Rect r = new Rect(sp.x - 100, sp.y - 40, 200, 80);
					GUI.Box(r, "", GUI.skin.window);
					GUILayout.BeginArea(r);
					GUILayout.BeginVertical();
					GUILayout.Space(10);
					GUILayout.Label("Add waypoint here?", EditorStyles.boldLabel);
					GUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();
					if (GUILayout.Button("Add", GUILayout.Width(80))) { AddWaypointAtTile(pendingAddTile); pendingAddTile = -1; }
					if (GUILayout.Button("Cancel", GUILayout.Width(80))) pendingAddTile = -1;
					GUILayout.FlexibleSpace();
					GUILayout.EndHorizontal();
					GUILayout.Space(10);
					GUILayout.EndVertical();
					GUILayout.EndArea();

					if (Input.GetMouseButtonDown(0))
					{
						Vector2 m = Input.mousePosition; m.y = Screen.height - m.y;
						if (!r.Contains(m)) pendingAddTile = -1;
					}
				}
				else pendingAddTile = -1;
			}

			// ——— DELETE CONFIRMATION POPUP ———
			if (pendingDeleteIndex >= 0 && pendingDeleteIndex < waypoints.Length)
			{
				var wp = waypoints[pendingDeleteIndex];
				Vector3 pos = editorController.iMapManager.TileWorldPosition(wp.tile) + Vector3.up * 0.8f;
				Vector3 sp = camera.WorldToScreenPoint(pos);
				if (sp.z > 0)
				{
					sp.y = Screen.height - sp.y;
					Rect r = new Rect(sp.x - 110, sp.y - 50, 220, 100);
					GUI.Box(r, "", GUI.skin.window);
					GUILayout.BeginArea(r);
					GUILayout.BeginVertical();
					GUILayout.Space(12);
					GUI.color = Color.red;
					GUILayout.Label("Delete waypoint?", EditorStyles.boldLabel);
					GUI.color = Color.white;
					GUILayout.Label($"WP{pendingDeleteIndex:00} at tile {wp.tile}");
					GUILayout.Space(8);
					GUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();
					if (GUILayout.Button("Delete", GUILayout.Width(90))) { DeleteWaypoint(pendingDeleteIndex); pendingDeleteIndex = -1; }
					if (GUILayout.Button("Cancel", GUILayout.Width(90))) pendingDeleteIndex = -1;
					GUILayout.FlexibleSpace();
					GUILayout.EndHorizontal();
					GUILayout.Space(8);
					GUILayout.EndVertical();
					GUILayout.EndArea();

					if (Input.GetMouseButtonDown(0))
					{
						Vector2 m = Input.mousePosition; m.y = Screen.height - m.y;
						if (!r.Contains(m)) pendingDeleteIndex = -1;
					}
				}
				else pendingDeleteIndex = -1;
			}
		}
	}
}