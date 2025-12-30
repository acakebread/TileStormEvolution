using System;
using System.Linq;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public class EditorControllerWaypoint : EditorControllerMovement
	{
		public EditorControllerWaypoint(EditorController editorController) : base(editorController) { }

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || AttachmentEditing.sidePanel.IsMouseOver;

		public override void OnMapLoaded()
		{
			AttachmentEditing.ResetInputState();
			AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Waypoint);
		}

		public override void OnEnable()
		{
			base.OnEnable();
			AttachmentEditing.OnEnableShared(iMapManager, EditorMarkerUtil.MarkerType.Waypoint);
		}

		public override void OnDisable()
		{
			base.OnDisable();
			AttachmentEditing.OnDisableShared();
		}

		public override void Update()
		{
			base.Update();
			AttachmentEditing.Update(camera, iMapManager, EditorMarkerUtil.MarkerType.Waypoint, IsMouseOverGUI());
		}

		public override void OnGUI()
		{
			DrawSidePanel();
			AttachmentEditing.OnGUI(iMapManager, camera);
		}

		private void DrawSidePanel()
		{
			Waypoint selectedWaypoint = null != AttachmentEditing.selectedAttachments && AttachmentEditing.selectedAttachments.Length > 0 ? AttachmentEditing.selectedAttachments[0] as Waypoint : null;
			var wpArray = currentMap.waypoints ?? Array.Empty<int>();
			var items = new List<ListViewItem>();

			var waypointAttachments = iMapManager.waypointAttachments; // This gives real Waypoint objects with correct indices

			for (int i = 0; i < wpArray.Length; i++)
			{
				int tile = wpArray[i];
				var waypoint = waypointAttachments.FirstOrDefault(w => w.waypointIndex == i);

				items.Add(new ListViewItem(
					label: $"WP{i:00} [tile {tile}]",
					onClick: (x) =>
					{
						// Select this waypoint
						if (null != waypoint)
							AttachmentEditing.Select(new[] { waypoint }, iMapManager, camera);
					},
					selected: selectedWaypoint?.waypointIndex == i
				));
			}

			AttachmentEditing.sidePanel.List.SetItems(items);

			// Buttons: Move Up / Down
			AttachmentEditing.sidePanel.Buttons.Clear();

			bool canMoveUp = selectedWaypoint != null && selectedWaypoint.waypointIndex > 0;
			bool canMoveDown = selectedWaypoint != null &&
							   selectedWaypoint.waypointIndex >= 0 &&
							   selectedWaypoint.waypointIndex < wpArray.Length - 1;

			AttachmentEditing.sidePanel.Buttons.Add(new ListViewButton("Move Up", () => MoveWaypoint(selectedWaypoint, -1), enabled: canMoveUp));
			AttachmentEditing.sidePanel.Buttons.Add(new ListViewButton("Move Down", () => MoveWaypoint(selectedWaypoint, +1), enabled: canMoveDown));

			AttachmentEditing.sidePanel.Draw();

			void MoveWaypoint(Waypoint wp, int direction)
			{
				if (wp == null) return;

				int oldIndex = wp.waypointIndex;
				int newIndex = oldIndex + direction;

				if (newIndex < 0 || newIndex >= currentMap.waypoints.Length) return;

				// Swap in the underlying waypoints array
				var list = currentMap.waypoints.ToList();
				(list[oldIndex], list[newIndex]) = (list[newIndex], list[oldIndex]);
				currentMap.waypoints = list.ToArray();

				// Update the waypoint objects' indices (important for future selections)
				// We need to refresh the virtual attachments so indices are correct
				// But since we're in editor, easiest is to re-select the moved one
				var movedWaypoint = new Waypoint(newIndex, list[newIndex]);
				AttachmentEditing.Select(new[] { movedWaypoint }, iMapManager, camera);

				// Rebuild markers to reflect new positions
				AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Waypoint);
			}
		}
	}
}
