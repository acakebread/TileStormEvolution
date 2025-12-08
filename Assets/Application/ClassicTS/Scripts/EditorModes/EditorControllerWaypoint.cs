using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using System;

namespace ClassicTilestorm
{
	public class EditorControllerWaypoint : EditorControllerMovement
	{
		public int SelectedWaypointIndex { get; private set; } = -1;

		private int draggingIndex = -1;
		private int originalTile = -1;

		private int pendingTile = -1;
		private int pendingWaypoint = -1;
		private enum PendingAction { None, Add, Delete }
		private PendingAction pendingAction = PendingAction.None;
		private Vector2 pendingPopupScreenPos = Vector2.zero;

		private Vector3 clickStartPos;
		private int clickStartTile = -1;
		private int potentialWaypointHit = -1;

		private Vector2 scrollPos = Vector2.zero;

		private readonly AutoHidePanel sidePanel = new(
			collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f);

		public override bool IsMouseOverModeGui()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Waypoint) return false;

			float w = sidePanel.CurrentWidth;
			var rect = new Rect(Screen.width - w - 20f, 20f, w, Screen.height - 40f);
			var mouse = Input.mousePosition; mouse.y = Screen.height - mouse.y;
			return rect.Contains(mouse);
		}

		public EditorControllerWaypoint(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();
			SelectedWaypointIndex = -1;
			EditorUtil.DestroyMarkerVisuals();
			RebuildMarkers();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			EditorUtil.DestroyMarkerVisuals();
			pendingAction = PendingAction.None;
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

			EditorUtil.UpdateMapMarkers(editorController.iMapManager, editorController.iMapManager.Waypoints, SelectedWaypointIndex, EditorUtil.MarkerType.Waypoint);
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

			var list = editorController.iMapManager.Waypoints?.ToList() ?? new List<int>();
			list.Add(tile);
			editorController.iMapManager.Waypoints = list.ToArray();

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

			if (camera == null || editorController.IsGuiControlActive() ||
				(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
				return;

			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			var snapped = editorController.iMapManager.SnappedMapPosition(worldPos);
			int tileUnderMouse = editorController.iMapManager.WorldToMapIndex(snapped);

			// === CLICK DETECTION (shared for left & right) ===
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			{
				clickStartPos = Input.mousePosition;
				clickStartTile = tileUnderMouse;
				potentialWaypointHit = IndexOfWaypoint(HitTile(Input.mousePosition));
			}

			// === LEFT CLICK RELEASE - ADD or DRAG ===
			if (Input.GetMouseButtonUp(0))
			{
				bool wasClick = Vector3.Distance(Input.mousePosition, clickStartPos) < 6f;

				if (draggingIndex >= 0)
				{
					// Was dragging a waypoint
					if (!wasClick && tileUnderMouse < 0)
					{
						// Revert if dropped outside map
						var map = editorController.iMapManager.CurrentMap;
						if (map?.waypoints != null && draggingIndex < map.waypoints.Length)
							editorController.iMapManager.Waypoints[draggingIndex] = originalTile;// map.waypoints[draggingIndex].tile = originalTile;
						RebuildMarkers();
					}
					draggingIndex = -1;
					originalTile = -1;
				}
				else if (wasClick && clickStartTile >= 0 && potentialWaypointHit < 0)
				{
					// True click on empty tile → prepare to add
					pendingTile = clickStartTile;
					pendingAction = PendingAction.Add;

					var _worldPos = editorController.iMapManager.TileWorldPosition(clickStartTile) + Vector3.up * 0.6f;
					var sp = camera.WorldToScreenPoint(_worldPos);
					sp.y = Screen.height - sp.y;
					pendingPopupScreenPos = sp;
				}
			}

			// === RIGHT CLICK RELEASE - DELETE ===
			if (Input.GetMouseButtonUp(1))
			{
				bool wasClick = Vector3.Distance(Input.mousePosition, clickStartPos) < 6f;

				if (wasClick && potentialWaypointHit >= 0)
				{
					SelectWaypoint(potentialWaypointHit);

					var map = editorController.iMapManager.CurrentMap;
					if (map?.waypoints != null && potentialWaypointHit < map.waypoints.Length)
					{
						pendingTile = editorController.iMapManager.Waypoints[potentialWaypointHit];
						pendingWaypoint = potentialWaypointHit;
						pendingAction = PendingAction.Delete;

						var _worldPos = editorController.iMapManager.TileWorldPosition(clickStartTile) + Vector3.up * 0.6f;
						var sp = camera.WorldToScreenPoint(_worldPos);
						sp.y = Screen.height - sp.y;
						pendingPopupScreenPos = sp;
					}
				}
			}

			// === DRAG EXISTING WAYPOINT (left button held) ===
			if (Input.GetMouseButton(0) && draggingIndex >= 0 && tileUnderMouse >= 0)
			{
				var map = editorController.iMapManager.CurrentMap;
				if (map?.waypoints != null && draggingIndex < map.waypoints.Length)
				{
					if (map.waypoints[draggingIndex] != tileUnderMouse)
					{
						map.waypoints[draggingIndex] = tileUnderMouse;
						RebuildMarkers();
					}
				}
			}

			// Start drag only if we clicked a waypoint and are now moving
			if (Input.GetMouseButtonDown(0) && potentialWaypointHit >= 0)
			{
				SelectWaypoint(potentialWaypointHit);
				draggingIndex = potentialWaypointHit;
				originalTile = editorController.iMapManager.Waypoints[potentialWaypointHit];
				pendingAction = PendingAction.None; // cancel any pending add
			}
		}

		private int IndexOfWaypoint(int tileIndex)
		{
			var map = editorController.currentMap;
			return null != map && null != map.waypoints && map.waypoints.Contains(tileIndex) ? Array.IndexOf(map.waypoints, tileIndex) : -1;
		}

		public override void OnGui()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Waypoint || camera == null) return;

			var map = editorController?.iMapManager?.CurrentMap;
			if (map == null) return;
			int[] waypoints = editorController?.iMapManager.Waypoints ?? System.Array.Empty<int>();

			sidePanel.Update();
			Rect panel = sidePanel.GetRect();

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
			else DrawWaypointList(waypoints, panel.height);

			GUILayout.EndVertical();
			GUILayout.EndArea();

			// ———————— POPUPS ————————
			if (pendingAction == PendingAction.Add)
				DrawAddPopup();

			if (pendingAction == PendingAction.Delete)
				DrawDeletePopup();
		}

		private GUIStyle leftButtonStyle; 
		private void DrawWaypointList(int[] waypoints, float panelHeight)
		{
			if (leftButtonStyle == null)
			{
				leftButtonStyle = new GUIStyle(GUI.skin.button);
				leftButtonStyle.alignment = TextAnchor.MiddleLeft;   // This is the key line
				leftButtonStyle.padding.left = 12;                  // Optional: nice left indent
			}

			const float reservedBottom = 110f;
			const float topOffset = 25f;
			float scrollHeight = Mathf.Max(0f, panelHeight - reservedBottom);

			Rect panelRect = sidePanel.GetRect();
			Rect scrollRect = new Rect(0f, topOffset, panelRect.width, scrollHeight);

			float scrollBarWidth = 12f;
			float contentWidth = scrollRect.width - scrollBarWidth;  // Critical: correct width

			Rect contentRect = new Rect(0f, 0f, scrollRect.width - scrollBarWidth - 10, waypoints.Length * 40f);

			scrollPos = GUI.BeginScrollView(scrollRect, scrollPos, contentRect, false, true);

			float y = 0f;
			for (int i = 0; i < waypoints.Length; i++)
			{
				var wp = waypoints[i];
				var vp = editorController?.iMapManager?.GetView(wp);
				string cam = vp != null ? " [Cam]" : "";
				string label = $"WP{i:00}{cam} [{wp}]";

				GUI.backgroundColor = (i == SelectedWaypointIndex)
					? new Color(0.3f, 0.8f, 1f, 0.9f)
					: Color.white;

				// FULL WIDTH BUTTON — starts at x=0, full content width
				if (GUI.Button(new Rect(0f, y, contentRect.width, 36f), label, leftButtonStyle))
					SelectWaypoint(i);

				GUI.backgroundColor = Color.white;

				y += 40f;
			}
			GUI.EndScrollView();

			// Bottom buttons
			Rect buttonsArea = new Rect(0f, scrollRect.y + scrollRect.height + 6f, 340f, reservedBottom);
			GUILayout.BeginArea(buttonsArea);
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			GUI.enabled = SelectedWaypointIndex > 0;
			if (GUILayout.Button("Move Up", GUILayout.Width(100), GUILayout.Height(30)))
				MoveWaypoint(SelectedWaypointIndex, -1);

			GUI.enabled = SelectedWaypointIndex >= 0 && SelectedWaypointIndex < waypoints.Length - 1;
			if (GUILayout.Button("Move Down", GUILayout.Width(100), GUILayout.Height(30)))
				MoveWaypoint(SelectedWaypointIndex, +1);

			GUI.enabled = true;
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			GUILayout.Space(4);
			GUILayout.Label("Tip: Left-click map to add • Right-click waypoint to delete",
				new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter });
			GUILayout.EndArea();
		}

		private void MoveWaypoint(int index, int direction)
		{
			var map = editorController.iMapManager.CurrentMap;
			var list = map.waypoints.ToList();
			var temp = list[index];
			list[index] = list[index + direction];
			list[index + direction] = temp;
			map.waypoints = list.ToArray();
			SelectedWaypointIndex += direction;
			RebuildMarkers();
		}

		private void DrawAddPopup()
		{
			var sp = pendingPopupScreenPos;
			sp.x -= 120;
			sp.y -= 110;

			if (PopupConfirm.Show(sp, new Vector2(240, 110), "Add waypoint here?",
				$"WP{pendingWaypoint:00} at tile {pendingTile}",
				yesText: "Add", noText: "Cancel", titleColor: Color.cyan,
				onYes: () => AddWaypointAtTile(pendingTile)))
			{
				pendingAction = PendingAction.None;
			}
		}

		private void DrawDeletePopup()
		{
			var sp = pendingPopupScreenPos;
			sp.x -= 120;
			sp.y -= 110;

			if (PopupConfirm.Show(sp, new Vector2(240, 110), "Delete waypoint?",
				$"WP{pendingWaypoint:00} at tile {pendingTile}",
				"Delete", "Cancel", Color.red,
				() => DeleteWaypoint(pendingWaypoint)))
			{
				pendingAction = PendingAction.None;
			}
		}
	}
}