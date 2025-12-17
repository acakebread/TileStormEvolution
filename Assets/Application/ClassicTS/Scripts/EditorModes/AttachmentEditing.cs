// AttachmentEditing.cs
using UnityEngine;
using System.Collections.Generic;
using static MassiveHadronLtd.GuiUtils;
using System.Linq;

namespace ClassicTilestorm
{
	public abstract class AttachmentEditing
	{
		// === Shared generic popups - static so any derived class can call them ===
		public static void DrawGUI(EditorControllerAttachment editor)
		{
			if (editor.CurrentPendingAction == EditorControllerAttachment.PendingAction.Add)
				DrawAddPopup(editor);
			if (editor.CurrentPendingAction == EditorControllerAttachment.PendingAction.Delete)
				DrawDeletePopup(editor);
			if (editor.CurrentPendingAction == EditorControllerAttachment.PendingAction.Select)
				DrawSelectPopup(editor);

			// Allow derived classes to draw type-specific GUI
			GetCurrentEditor(editor)?.DrawTypeSpecificGUI(editor);
		}

		// Virtual methods - override in derived classes when needed
		protected virtual void DrawTypeSpecificGUI(EditorControllerAttachment editor) { }

		public virtual void HandleSelectionChanged(EditorControllerAttachment editor) { }
		public virtual void HandleDrag(EditorControllerAttachment editor, MapAttachment attachment) { }
		public virtual void HandleGizmoInput(EditorControllerAttachment editor) { }

		// === Generic popup implementations (exactly as before) ===

		private static void DrawAddPopup(EditorControllerAttachment editor)
		{
			var items = new List<PopupItem>
			{
				//new PopupItem("Emitter [flame]", () =>
				//{
				//	var emitter = editor.AddNewAttachment(editor.PendingTile, typeof(Emitter)) as Emitter;
				//	if (emitter != null)
				//		emitter.variant = "flame";
				//}),
				//new PopupItem("Emitter [spark]", () =>
				//{
				//	var emitter = editor.AddNewAttachment(editor.PendingTile, typeof(Emitter)) as Emitter;
				//	if (emitter != null)
				//		emitter.variant = "spark";
				//}),

				new PopupItem("Emitter [flame]", () =>
				{
					AttachmentEmitterEditing.Instance.AddNewEmitter(editor, editor.PendingTile, "flame");
				}),
				new PopupItem("Emitter [spark]", () =>
				{
					AttachmentEmitterEditing.Instance.AddNewEmitter(editor, editor.PendingTile, "spark");
				}),
				new PopupItem("View", () => editor.AddNewAttachment(editor.PendingTile, typeof(View))),
				new PopupItem("Pickup", () => editor.AddNewAttachment(editor.PendingTile, typeof(Pickup))),
				PopupItem.Spacer(),
				new PopupItem("Cancel", () => editor.SelectAttachments(null), colorOverride: Color.yellow)
			};

			bool closed = PopupMenu.Show(editor.PendingPopupScreenPos, "Add Attachment", items);
			if (closed) editor.ClearPendingAction(false);//do not clear the selection - the gizmo editor needs it!!! - whole system needs a rethink / refactor
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

					if (localAtt is Emitter emitter)
					{
						editor.editorController.iMapManager.DestroyEmitterInstance(emitter);
					}
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

					var removedEmitters = attsOnTile.OfType<Emitter>();
					foreach (var e in removedEmitters)
					{
						editor.editorController.iMapManager.DestroyEmitterInstance(e);
					}

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
				editor.ClearPendingAction();
			}
		}

		// Helper to get the correct derived editor based on selection
		public static AttachmentEditing GetCurrentEditor(EditorControllerAttachment editor)
		{
			if (editor.selectedAttachments == null || editor.selectedAttachments.Length == 0)
				return null;

			// Prioritize View because it has the most visual editing needs
			if (editor.selectedAttachments.Any(att => att is View))
				return AttachmentViewEditing.Instance;

			// Otherwise, use the primary (first) attachment type
			var primary = editor.selectedAttachments[0];
			return primary switch
			{
				Emitter => AttachmentEmitterEditing.Instance,
				Pickup => AttachmentPickupEditing.Instance,
				_ => null
			};
		}
	}
}