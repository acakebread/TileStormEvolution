using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorAttachmentUI
	{
		// ─────────────────────────────────────────────────────────────────────
		// Moved/shared pending state
		// ─────────────────────────────────────────────────────────────────────
		private enum PendingAction { None, Add, Delete, Select }
		private static PendingAction pendingAction = PendingAction.None;
		private static Vector3 popupPos;

		// Public API to enter pending mode + capture mouse position
		public static void RequestAdd()
		{
			pendingAction = PendingAction.Add;
			popupPos = InputX.mousePosition;
		}

		public static void RequestDelete()
		{
			pendingAction = PendingAction.Delete;
			popupPos = InputX.mousePosition;
		}

		public static void RequestSelect()
		{
			pendingAction = PendingAction.Select;
			popupPos = InputX.mousePosition;
		}

		public static void ClearPending()
		{
			pendingAction = PendingAction.None;
		}

		public static bool IsPending => pendingAction != PendingAction.None;

		// ─────────────────────────────────────────────────────────────────────
		// Side panels (unchanged)
		// ─────────────────────────────────────────────────────────────────────
		public static readonly AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f));

		private static void DrawSidePanelAttachment(IMapEdit iMap, MapAttachment[] selection, Action<MapAttachment> onSelect)
		{
			var atts = iMap?.GetAttachments() ?? Array.Empty<MapAttachment>();

			var items = new List<ListViewItem>();

			foreach (var att in atts)
			{
				items.Add(new ListViewItem(
					GetAttachmentLabel(att),
					(_) => onSelect?.Invoke(att),
					selected: selection != null && selection.Contains(att)
				));
			}

			sidePanel.List.SetItems(items);
			sidePanel.SetFootnote("Hold RMB on preview to orbit • Scroll to zoom • LMB: place/move • RMB on tile: delete");
			sidePanel.Draw();

			static string GetAttachmentLabel(MapAttachment att) => att switch
			{
				Emitter e => $"Emitter [{att.tile}]" + (e.LookAt.sqrMagnitude > 0.01f && e.LookAt != Vector3.up ? $" → {e.LookAt.magnitude:F1}" : ""),
				View => $"View [{att.tile}]",
				Pickup p => $"Pickup [{att.tile}] ({p.amount})",
				_ => $"{att.TypeName} [{att.tile}]"
			};
		}

		private static void DrawSidePanelWaypoint(IMapEdit iMap, MapAttachment[] selection, Action<Waypoint> onSelectWaypoint)
		{
			var selectedWaypoint = selection?.Length > 0 ? selection[0] as Waypoint : null;

			var waypointAttachments = iMap.GetAttachments(filterTypes: new[] { typeof(Waypoint) })
										  .Cast<Waypoint>()
										  .OrderBy(wp => wp.waypointIndex)
										  .ToArray();

			var items = new List<ListViewItem>();

			foreach (var waypoint in waypointAttachments)
			{
				items.Add(new ListViewItem(
					label: $"WP{waypoint.waypointIndex:00} [tile {waypoint.tile}]",
					onClick: (_) => onSelectWaypoint?.Invoke(waypoint),
					selected: selectedWaypoint?.waypointIndex == waypoint.waypointIndex
				));
			}

			sidePanel.List.SetItems(items);
			sidePanel.Buttons.Clear();

			var canMoveUp = selectedWaypoint != null && selectedWaypoint.waypointIndex > 0;
			var canMoveDown = selectedWaypoint != null && selectedWaypoint.waypointIndex < waypointAttachments.Length - 1;

			sidePanel.Buttons.Add(new("Move Up", () => onSelectWaypoint?.Invoke(MoveWaypoint(selectedWaypoint, -1)), enabled: canMoveUp));
			sidePanel.Buttons.Add(new("Move Down", () => onSelectWaypoint?.Invoke(MoveWaypoint(selectedWaypoint, +1)), enabled: canMoveDown));

			sidePanel.Draw();

			Waypoint MoveWaypoint(Waypoint wp, int direction)
			{
				if (wp == null) return null;

				var oldIndex = wp.waypointIndex;
				var newIndex = oldIndex + direction;

				var currentWaypoints = iMap.GetWaypoints();

				if (newIndex < 0 || newIndex >= currentWaypoints.Length) return null;

				var targetWp = currentWaypoints[newIndex];

				wp.waypointIndex = newIndex;
				targetWp.waypointIndex = oldIndex;

				return new Waypoint(newIndex, wp.tile);
			}
		}

		// ─────────────────────────────────────────────────────────────────────
		// Popups — now take tile as parameter, use static popupPos + mode check
		// ─────────────────────────────────────────────────────────────────────
		private static bool DrawAddPopup(IMapEdit iMap, int tile, Action<MapAttachment> onCreateAndSelect)
		{
			if (pendingAction != PendingAction.Add) return false;

			var waypoints = iMap.GetWaypoints();

			var items = new List<PopupItem>
			{
				new($"Waypoint [WP{waypoints?.Length:00}]", () => onCreateAndSelect(Waypoint.Create(iMap, tile)), colorOverride: Color.lightSteelBlue),
				new("Emitter [flame]", () => onCreateAndSelect(Emitter.Create(iMap, tile, "flame")), colorOverride: Color.cyan),
				new("Emitter [spark]", () => onCreateAndSelect(Emitter.Create(iMap, tile, "spark")), colorOverride: Color.cyan),
				new("View",            () => onCreateAndSelect(View.Create(iMap, tile)),            colorOverride: Color.cyan),
				new("Pickup",          () => onCreateAndSelect(Pickup.Create(iMap, tile)),          colorOverride: Color.cyan),
				PopupItem.Spacer(),
				new("Cancel", () => { }, colorOverride: Color.yellow)
			};

			var result = PopupMenu.Show(popupPos, $"Add Attachment at tile {tile}", items);

			if (result == PopupResult.ClosedByAction)
				return false;

			return result == PopupResult.StillOpen;
		}

		private static bool DrawDeletePopup(IMapEdit iMap, int tile, Action<MapAttachment[]> onSelect)
		{
			if (pendingAction != PendingAction.Delete) return false;

			var attsOnTile = iMap.GetAttachments(tileIndex: tile);
			if (attsOnTile.Length == 0) return false;

			var items = new List<PopupItem>();

			foreach (var att in attsOnTile)
			{
				var localAtt = att;
				string label = att is Waypoint wp ? $"Delete WP{wp.waypointIndex:00} [{tile}]" : $"Delete {att.GetType().Name} [{tile}]";
				items.Add(new PopupItem(label, () => iMap.RemoveAttachment(localAtt), colorOverride: Color.softRed));
			}

			if (attsOnTile.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Delete All", () => iMap.RemoveAttachments(attsOnTile), colorOverride: Color.red));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			var result = PopupMenu.Show(popupPos, "Delete Attachment" + (attsOnTile.Length > 1 ? "(s)" : ""), items);

			if (result != PopupResult.StillOpen)
				onSelect(null);

			return result == PopupResult.StillOpen;
		}

		private static bool DrawSelectPopup(IMapEdit iMap, int tile, Action<MapAttachment[]> onSelect)
		{
			if (pendingAction != PendingAction.Select) return false;

			var atts = iMap.GetAttachments(tileIndex: tile);
			if (atts.Length == 0) return false;

			var items = new List<PopupItem>();

			foreach (var att in atts)
			{
				string label = att.GetType().Name;
				if (att is Emitter e && e.LookAt != null && e.LookAt != Vector3.up)
					label += $" to {e.LookAt.magnitude:F1}";
				label += $" [tile {att.tile}]";

				items.Add(new PopupItem(label, () => onSelect(new[] { att })));
			}

			if (atts.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Select All", () => onSelect(atts), colorOverride: Color.green));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			var result = PopupMenu.Show(popupPos, $"Select ({atts.Length})", items);

			if (result == PopupResult.ClosedByAction)
				return false;

			if (result == PopupResult.ClosedByClickOutside || result == PopupResult.ClosedByCancel)
				onSelect(null);

			return result == PopupResult.StillOpen;
		}

		public static bool EvaluateSelection(ISelectable[] selection, int tile)
		{
			var atts = selection?.OfType<MapAttachment>().ToArray();
			if (atts == null || atts.Length == 0)
			{
				if (tile != -1)
					RequestAdd();
			}
			else if (atts.Length > 1)
			{
				RequestSelect();
			}
			else
			{
				ClearPending();
				return true;
			}
			return false;
		}

		public static void UpdateGUI(IMapEdit iMap, ISelectable[] selection, int tileIndex, Action<ISelectable[]> onSelect)
		{
			var currentSelection = selection?.OfType<MapAttachment>().ToArray();

			// Side panel handling
			if (currentSelection != null && currentSelection.Length > 0)
			{
				if (currentSelection.Length == 1 && currentSelection[0] is Waypoint wp)
				{
					DrawSidePanelWaypoint(iMap, currentSelection,
						waypoint => onSelect(new ISelectable[] { waypoint }));
				}
				else
				{
					DrawSidePanelAttachment(iMap, currentSelection,
						att => onSelect(new ISelectable[] { att }));
				}
			}
			else
			{
				sidePanel.Update();
				sidePanel.IsMouseOver = false;
			}

			// Popups ── wrap/unwrap the array
			bool stillOpen =
				DrawAddPopup(iMap, tileIndex, created => onSelect(new ISelectable[] { created })) ||
				DrawDeletePopup(iMap, tileIndex, atts => onSelect(atts?.Cast<ISelectable>().ToArray())) ||
				DrawSelectPopup(iMap, tileIndex, atts => onSelect(atts?.Cast<ISelectable>().ToArray()));

			if (!stillOpen && pendingAction != PendingAction.None)
				ClearPending();
		}
	}
}