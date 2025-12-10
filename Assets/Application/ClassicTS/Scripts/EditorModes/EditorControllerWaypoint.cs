using System.Linq;
using UnityEngine;
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

		private readonly AutoHidePanel sidePanel = new(120f, 340f, 1.5f, 0.25f);

		public override bool IsMouseOverGui()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Waypoint) return false;

			Rect panelRect = sidePanel.GetPanelRect();
			Vector2 mouse = Input.mousePosition;
			mouse.y = Screen.height - mouse.y;

			return panelRect.Contains(mouse);
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
			if (!editorCamera || IsMouseOverGui() || IsGuiControlActive()) return;
			base.Update();

			var worldPos = MapManager.ScreenToWorld(editorCamera, Input.mousePosition);
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
					var sp = editorCamera.WorldToScreenPoint(_worldPos);
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
						var sp = editorCamera.WorldToScreenPoint(_worldPos);
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

		public override void OnGUI()
		{
			if (editorController.CurrentMode != EditorController.EditorMode.Waypoint)
				return;

			sidePanel.Update();

			// Build ListView items
			var wp = editorController.iMapManager.Waypoints ?? Array.Empty<int>();
			var items = new List<ListViewItem>();

			for (int i = 0; i < wp.Length; i++)
			{
				int idx = i;
				bool selected = idx == SelectedWaypointIndex;

				items.Add(new ListViewItem(
					label: $"WP{idx:00} [{wp[i]}]",
					onClick: () => SelectWaypoint(idx),
					selected: selected
				));
			}

			sidePanel.List.SetItems(items);

			// Build dynamic Move buttons
			sidePanel.Buttons.Clear();

			// Up button always visible
			sidePanel.Buttons.Add(new ListViewButton(
				"Move Up",
				() => MoveWaypoint(SelectedWaypointIndex, -1),
				enabled: SelectedWaypointIndex > 0
			));

			// Down button always visible
			sidePanel.Buttons.Add(new ListViewButton(
				"Move Down",
				() => MoveWaypoint(SelectedWaypointIndex, +1),
				enabled: SelectedWaypointIndex >= 0 && SelectedWaypointIndex < wp.Length - 1
			));

			// Draw entire panel
			sidePanel.Draw();

			// Popups
			if (pendingAction == PendingAction.Add) DrawAddPopup();
			if (pendingAction == PendingAction.Delete) DrawDeletePopup();
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

			var items = new List<PopupItem>
			{
				// Info line (non-clickable)
				new PopupItem($"WP{editorController.iMapManager.Waypoints.Length:00} at tile {pendingTile}", null, null, spacerHeight: 0),

				PopupItem.Spacer(6),

				// Add waypoint
				new PopupItem("Add", () =>
				{
					AddWaypointAtTile(pendingTile);
				}, colorOverride: Color.cyan),

				PopupItem.Spacer(4),

				// Cancel
				new PopupItem("Cancel", null, Color.yellow)
			};

			bool closed = PopupMenu.Show(sp, "Add Waypoint?", items);

			if (closed)
				pendingAction = PendingAction.None;
		}

		private void DrawDeletePopup()
		{
			var sp = pendingPopupScreenPos;
			sp.x -= 120;
			sp.y -= 110;

			var items = new List<PopupItem>
			{
				// Info line (non-clickable)
				new PopupItem($"WP{pendingWaypoint:00} at tile {pendingTile}", null, null, spacerHeight: 0),

				PopupItem.Spacer(6),

				// Delete waypoint
				new PopupItem("Delete", () =>
				{
					DeleteWaypoint(pendingWaypoint);
				}, colorOverride: Color.red),

				PopupItem.Spacer(4),

				// Cancel
				new PopupItem("Cancel", null, Color.yellow)
			};

			bool closed = PopupMenu.Show(sp, "Delete Waypoint?", items);

			if (closed)
				pendingAction = PendingAction.None;
		}
	}
}