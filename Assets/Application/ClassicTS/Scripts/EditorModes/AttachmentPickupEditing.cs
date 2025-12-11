// AttachmentPickupEditing.cs
namespace ClassicTilestorm
{
	public class AttachmentPickupEditing : AttachmentEditing
	{
		public static readonly AttachmentPickupEditing Instance = new();

		public override void HandleSelectionChanged(EditorControllerAttachment editor) { }
		public override void HandleDrag(EditorControllerAttachment editor, MapAttachment attachment) { }
		protected override void DrawTypeSpecificGUI(EditorControllerAttachment editor) { }
	}
}