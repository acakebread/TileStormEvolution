using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public static class EditorAttachmentUI
	{
		// Side panels

		public static void DrawSidePanelAttachment(
			AutoHidePanel sidePanel,
			IMapEdit iMap,
			MapAttachment[] selection,
			Action<MapAttachment> onSelect)
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

		public static void DrawSidePanelWaypoint(
			AutoHidePanel sidePanel,
			IMapEdit iMap,
			MapAttachment[] selection,
			Action<Waypoint, int> onMoveWaypoint)
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
					// No onClick — matches original behavior (selection via map, not list)
					selected: selectedWaypoint?.waypointIndex == waypoint.waypointIndex
				));
			}

			sidePanel.List.SetItems(items);
			sidePanel.Buttons.Clear();

			var canMoveUp = selectedWaypoint != null && selectedWaypoint.waypointIndex > 0;
			var canMoveDown = selectedWaypoint != null && selectedWaypoint.waypointIndex < waypointAttachments.Length - 1;

			// ── Use exactly the same syntax as your original code ──
			sidePanel.Buttons.Add(new("Move Up", () => onMoveWaypoint?.Invoke(selectedWaypoint, -1), enabled: canMoveUp));
			sidePanel.Buttons.Add(new("Move Down", () => onMoveWaypoint?.Invoke(selectedWaypoint, +1), enabled: canMoveDown));

			sidePanel.Draw();
		}

		// Popups

		public static bool DrawAddPopup(
			Vector3 popupPos,
			IMapEdit iMap,
			int pendingTile,
			Action<MapAttachment> onCreateAndSelect)
		{
			var waypoints = iMap.GetWaypoints();

			var items = new List<PopupItem>
			{
				new($"Waypoint [WP{waypoints?.Length:00}]", () => onCreateAndSelect(WaypointAttachmentHandler.Create(iMap, pendingTile)), colorOverride: Color.lightSteelBlue),
				new("Emitter [flame]", () => onCreateAndSelect(EmitterAttachmentHandler.Create(iMap, pendingTile, "flame")), colorOverride: Color.cyan),
				new("Emitter [spark]", () => onCreateAndSelect(EmitterAttachmentHandler.Create(iMap, pendingTile, "spark")), colorOverride: Color.cyan),
				new("View",            () => onCreateAndSelect(ViewAttachmentHandler.Create(iMap, pendingTile)),            colorOverride: Color.cyan),
				new("Pickup",          () => onCreateAndSelect(PickupAttachmentHandler.Create(iMap, pendingTile)),          colorOverride: Color.cyan),
				PopupItem.Spacer(),
				new("Cancel", () => { }, colorOverride: Color.yellow)
			};

			var result = PopupMenu.Show(popupPos, $"Add Attachment at tile {pendingTile}", items);

			if (result == PopupResult.ClosedByAction)
				return false;

			// We no longer call Select() here — caller will handle deselect if needed
			return result == PopupResult.StillOpen;
		}

		public static bool DrawDeletePopup(
			Vector3 popupPos,
			IMapEdit iMap,
			int pendingTile,
			Action onDeselect)
		{
			var attsOnTile = iMap.GetAttachments(tileIndex: pendingTile);
			if (attsOnTile.Length == 0) return false;

			var items = new List<PopupItem>();

			foreach (var att in attsOnTile)
			{
				var localAtt = att;
				string label = att is Waypoint wp ? $"Delete WP{wp.waypointIndex:00} [{pendingTile}]" : $"Delete {att.GetType().Name} [{pendingTile}]";
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
				onDeselect?.Invoke();

			return result == PopupResult.StillOpen;
		}

		public static bool DrawSelectPopup(
			Vector3 popupPos,
			IMapEdit iMap,
			int pendingTile,
			Action<MapAttachment> onSelect,
			Action<MapAttachment[]> onSelectMultiple,
			Action onDeselect)
		{
			var atts = iMap.GetAttachments(tileIndex: pendingTile);
			if (atts.Length == 0) return false;

			var items = new List<PopupItem>();

			foreach (var att in atts)
			{
				string label = att.GetType().Name;
				if (att is Emitter e && e.LookAt != null && e.LookAt != Vector3.up)
					label += $" to {e.LookAt.magnitude:F1}";
				label += $" [tile {att.tile}]";

				items.Add(new PopupItem(label, () => onSelect(att)));
			}

			if (atts.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Select All", () => onSelectMultiple(atts), colorOverride: Color.green));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", () => { }, colorOverride: Color.yellow));

			var result = PopupMenu.Show(popupPos, $"Select ({atts.Length})", items);

			if (result == PopupResult.ClosedByAction)
				return false;

			if (result == PopupResult.ClosedByClickOutside || result == PopupResult.ClosedByCancel)
				onDeselect?.Invoke();

			return result == PopupResult.StillOpen;
		}
	}
}