using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using System.Linq;
using System;

namespace ClassicTilestorm
{
	public abstract class AttachmentEditing
	{
		private static readonly AutoHidePanel sidePanel = new AutoHidePanel(
			collapsed: 120f, expanded: 340f, delay: 1.5f, animDur: 0.25f, defaultPos: new Vector2(0f, 40f));

		public static bool IsMouseOverSidePanel()
		{
			Rect panelRect = sidePanel.GetPanelRect();
			Vector2 mouse = Input.mousePosition;
			mouse.y = Screen.height - mouse.y;
			return panelRect.Contains(mouse);
		}

		private static string GetAttachmentLabel(MapAttachment att)
		{
			return att switch
			{
				Emitter e => $"Emitter [{att.tile}]" +
							 (e.LookAt.sqrMagnitude > 0.01f && e.LookAt != Vector3.up
							  ? $" → {e.LookAt.magnitude:F1}"
							  : ""),
				View => $"View [{att.tile}]",
				Pickup p => $"Pickup [{att.tile}] ({p.amount})",
				_ => $"{att.TypeName} [{att.tile}]"
			};
		}

		public static void DrawGUI(EditorControllerAttachment editor)
		{
			if (editor.CurrentPendingAction == EditorControllerAttachment.PendingAction.Add)
				DrawAddPopup(editor);
			if (editor.CurrentPendingAction == EditorControllerAttachment.PendingAction.Delete)
				DrawDeletePopup(editor);
			if (editor.CurrentPendingAction == EditorControllerAttachment.PendingAction.Select)
				DrawSelectPopup(editor);

			// === SIDE PANEL: Now owned here ===
			sidePanel.Update();
			sidePanel.List.Clear();

			var map = editor.editorController?.iMapManager?.CurrentMap;
			if (map != null && map.attachments != null)
			{
				foreach (var att in map.attachments)
				{
					string label = GetAttachmentLabel(att);

					sidePanel.List.AddItem(new ListViewItem(
						label,
						() => editor.SelectAttachments(new[] { att }),
						selected: editor.selectedAttachments != null && editor.selectedAttachments.Contains(att)
					));
				}
			}

			sidePanel.SetFootnote("Hold RMB on preview to orbit • Scroll to zoom • LMB: place/move • RMB on tile: delete");
			sidePanel.Draw();

			// === TYPE-SPECIFIC GUI (e.g. future sliders, buttons) ===
			GetEditorForSelection(editor.selectedAttachments)?.DrawTypeSpecificGUI(editor);
		}

		// Virtual methods - override in derived classes when needed
		protected virtual void DrawTypeSpecificGUI(EditorControllerAttachment editor) { }//currently no implementations but will be used for camera FOV / emitter params etc

		public static void HandleGizmoInput(EditorControllerAttachment editor) => GetEditorForSelection(editor.selectedAttachments)?.OnHandleGizmoInput(editor);
		protected virtual void OnHandleGizmoInput(EditorControllerAttachment editor) { }

		public static void HandleSelectionChanged(EditorControllerAttachment editor) => GetEditorForSelection(editor.selectedAttachments)?.OnHandleSelectionChanged(editor);
		protected virtual void OnHandleSelectionChanged(EditorControllerAttachment editor) { }

		protected virtual void OnRefreshDragVisuals(EditorControllerAttachment editor, MapAttachment attachment) { }

		protected virtual void OnUpdateDragGizmo(EditorControllerAttachment editor, MapAttachment attachment)
		{
			if (attachment is not ITransformableAttachment transformable)
				return;

			var worldPos = MapManager.WorldPosition(attachment.tile, transformable.Position);
			var worldRot = MapManager.WorldRotation(attachment.tile, transformable.Rotation);

			EditorTransformUtil.ShowAt(worldPos, worldRot, editor.editorCamera);
		}

		private static void DrawAddPopup(EditorControllerAttachment editor)
		{
			var items = new List<PopupItem>
			{
				new ("Emitter [flame]", () => { AttachmentEmitterEditing.Instance.AddNewEmitter(editor, editor.PendingTile, "flame"); }),
				new ("Emitter [spark]", () => { AttachmentEmitterEditing.Instance.AddNewEmitter(editor, editor.PendingTile, "spark"); }),
				new ("View", () => { AttachmentViewEditing.Instance.AddNewView(editor, editor.PendingTile); }),
				new ("Pickup", () => { AttachmentPickupEditing.Instance.AddNewPickup(editor, editor.PendingTile); }),
				PopupItem.Spacer(),
				new ("Cancel", colorOverride: Color.yellow)
			};

			bool closed = PopupMenu.Show(editor.PendingPopupScreenPos, "Add Attachment", items);
			if (closed)
				editor.ClearPendingAction(false); // keep selection for gizmo
		}

		private static void DrawDeletePopup(EditorControllerAttachment editor)
		{
			var map = editor.editorController?.iMapManager?.CurrentMap;
			if (map == null) return;

			var attsOnTile = map.GetAttachmentsOnTile(editor.PendingTile);
			if (attsOnTile.Length == 0) return;

			var items = new List<PopupItem>();

			foreach (var att in attsOnTile)
			{
				string label = $"Delete {att.GetType().Name}";
				var localAtt = att;
				items.Add(new PopupItem(label, () =>
				{
					map.RemoveAttachment(localAtt);

					editor.editorController.iMapManager.DestroyAttachmentInstance(localAtt);

					editor.SelectAttachments(null);
					EditorPrimitiveUtil.HideCone();
					EditorFrustumUtil.Hide();
					EditorTransformUtil.HideTransformGizmo();
					editor.RebuildMarkers();
					editor.viewPreview.Hide();
					editor.editorController.OnMapChanged();
				}));
			}

			if (attsOnTile.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Delete All", () =>
				{
					map.RemoveAllAttachmentsOnTile(editor.PendingTile);

					foreach (var att in attsOnTile)
						editor.editorController.iMapManager.DestroyAttachmentInstance(att);

					editor.SelectAttachments(null);
					EditorPrimitiveUtil.HideCone();
					EditorFrustumUtil.Hide();
					EditorTransformUtil.HideTransformGizmo();
					editor.RebuildMarkers();
					editor.viewPreview.Hide();
					editor.editorController.OnMapChanged();
				}, colorOverride: Color.red));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", colorOverride: Color.yellow));

			bool closed = PopupMenu.Show(editor.PendingPopupScreenPos, "Delete Attachment(s)", items);
			if (closed) editor.ClearPendingAction();
		}

		private static void DrawSelectPopup(EditorControllerAttachment editor)
		{
			var map = editor.editorController.iMapManager.CurrentMap;
			var atts = map.GetAttachmentsOnTile(editor.PendingTile);
			if (atts == null || atts.Length == 0) return;

			var items = new List<PopupItem>();

			foreach (var att in atts)
			{
				string label = att.GetType().Name;
				if (att is Emitter e && e.LookAt != null && e.LookAt != Vector3.up)
					label += $" to {e.LookAt.magnitude:F1}";
				label += $" [tile {att.tile}]";

				items.Add(new PopupItem(label, () =>
				{
					editor.pendingAction = EditorControllerAttachment.PendingAction.Drag;
					editor.SelectAttachments(new MapAttachment[] { att });
				}));
			}

			if (atts.Length > 1)
			{
				items.Add(PopupItem.Spacer());
				items.Add(new PopupItem("Select All", () =>
				{
					editor.pendingAction = EditorControllerAttachment.PendingAction.Drag;
					editor.SelectAttachments(atts);
				}, colorOverride: Color.green));
			}

			items.Add(PopupItem.Spacer());
			items.Add(new PopupItem("Cancel", colorOverride: Color.yellow));

			bool closed = PopupMenu.Show(editor.PendingPopupScreenPos, $"Select ({atts.Length})", items);
			if (closed)
			{
				if (editor.pendingAction != EditorControllerAttachment.PendingAction.Drag)
					editor.SelectAttachments(null);
				editor.ClearPendingAction(false);
			}
		}

		private static AttachmentEditing GetEditorFor(MapAttachment attachment)
		{
			if (attachment == null) return null;

			return attachment switch
			{
				Emitter => AttachmentEmitterEditing.Instance,
				View => AttachmentViewEditing.Instance,
				Pickup => AttachmentPickupEditing.Instance,
				_ => null
			};
		}

		// Optional: Only if you need it for uniform selections (e.g. type-specific GUI when all same type)
		private static AttachmentEditing GetEditorForSelection(MapAttachment[] selectedAttachments)
		{
			if (selectedAttachments == null || selectedAttachments.Length == 0)
				return null;

			var firstType = selectedAttachments[0].GetType();
			if (selectedAttachments.All(att => att.GetType() == firstType))
			{
				return GetEditorFor(selectedAttachments[0]);
			}

			return null; // mixed → no shared editor
		}

		public static void RefreshDragVisuals(EditorControllerAttachment editor)
		{
			if (editor?.selectedAttachments == null || editor.selectedAttachments.Length == 0) return;

			// Always refresh runtime GameObjects (particles, etc.) — safe for all types
			foreach (var att in editor.selectedAttachments)
				editor.editorController.iMapManager.RefreshAttachmentInstance(att);

			// === ONLY if SINGLE selection: update type-specific helpers and gizmo ===
			if (editor.selectedAttachments.Length == 1)
			{
				var att = editor.selectedAttachments[0];
				var typeEditor = GetEditorFor(att);
				typeEditor?.OnRefreshDragVisuals(editor, att);
				typeEditor?.OnUpdateDragGizmo(editor, att);
			}
			else
			{
				// MULTI selection: explicitly hide everything
				EditorTransformUtil.HideTransformGizmo();
				EditorPrimitiveUtil.HideCone();
				editor.viewPreview.Hide();
				EditorFrustumUtil.Hide(); // if you have this method
			}
		}

		public static void UpdateMapMarkers(IMapManager mapManager, int[] tiles, int selectedIndex = -1, EditorMarkerUtil.MarkerType type = EditorMarkerUtil.MarkerType.Undefined)
		{
			if (tiles == null || tiles.Length == 0 || EditorMarkerUtil.SphereMesh == null)
			{
				EditorMarkerUtil.ClearMapMarkers();
				return;
			}

			var positions = new Vector3[tiles.Length];
			var colors = new Color[tiles.Length];

			for (int i = 0; i < tiles.Length; i++)
			{
				int tile = tiles[i];
				if (tile < 0 || tile >= mapManager.Count)
				{
					positions[i] = Vector3.zero;
					colors[i] = new Color(0f, 0.7f, 1f, 0.7f);
					continue;
				}

				positions[i] = mapManager.TileWorldPosition(tile);

				bool hasView = type == EditorMarkerUtil.MarkerType.Waypoint && mapManager.GetView(tile) != null;
				colors[i] = hasView ? new Color(0f, 1f, 1f, 0.5f) : new Color(0f, 0.7f, 1f, 0.7f);
			}

			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}
	}
}