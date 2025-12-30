using UnityEngine;
using System.Linq;
using static MassiveHadronLtd.GuiUtils;

namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || AttachmentEditing.sidePanel.IsMouseOver;

		public override void OnMapLoaded()
		{
			AttachmentEditing.ResetInputState();
			AttachmentEditing.RebuildMarkers(iMapManager, EditorMarkerUtil.MarkerType.Attachment);
		}

		public override void OnEnable()
		{
			base.OnEnable();
			AttachmentEditing.OnEnableShared(iMapManager, EditorMarkerUtil.MarkerType.Attachment);
		}

		public override void OnDisable()
		{
			base.OnDisable();
			AttachmentEditing.OnDisableShared();
		}

		public override void Update()
		{
			base.Update();
			AttachmentEditing.Update(camera, iMapManager, EditorMarkerUtil.MarkerType.Attachment, IsMouseOverGUI());
		}

		public override void OnGUI()
		{
			DrawSidePanel();
			AttachmentEditing.OnGUI(iMapManager, camera);
		}

		private void DrawSidePanel()
		{
			var atts = currentMap.attachments ?? System.Array.Empty<MapAttachment>();
			var items = new System.Collections.Generic.List<ListViewItem>();
			foreach (var att in atts)
				items.Add(new ListViewItem(GetAttachmentLabel(att), (x) => AttachmentEditing.Select(new[] { att }, iMapManager, camera), selected: null != AttachmentEditing.selectedAttachments && AttachmentEditing.selectedAttachments.Contains(att)));
			AttachmentEditing.sidePanel.List.SetItems(items);
			AttachmentEditing.sidePanel.SetFootnote("Hold RMB on preview to orbit • Scroll to zoom • LMB: place/move • RMB on tile: delete");
			AttachmentEditing.sidePanel.Draw();

			static string GetAttachmentLabel(MapAttachment att) => att switch
			{
				Emitter e => $"Emitter [{att.tile}]" + (e.LookAt.sqrMagnitude > 0.01f && e.LookAt != Vector3.up ? $" → {e.LookAt.magnitude:F1}" : ""),
				View => $"View [{att.tile}]",
				Pickup p => $"Pickup [{att.tile}] ({p.amount})",
				_ => $"{att.TypeName} [{att.tile}]"
			};
		}
	}
}
