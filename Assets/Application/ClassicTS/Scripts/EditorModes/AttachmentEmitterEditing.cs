// AttachmentEmitterEditing.cs
namespace ClassicTilestorm
{
	public class AttachmentEmitterEditing : AttachmentEditing
	{
		public static readonly AttachmentEmitterEditing Instance = new();

		// Override when needed
		public override void HandleSelectionChanged(EditorControllerAttachment editor) { }
		public override void HandleDrag(EditorControllerAttachment editor, MapAttachment attachment) { }
		protected override void DrawTypeSpecificGUI(EditorControllerAttachment editor) { }
	}
}
