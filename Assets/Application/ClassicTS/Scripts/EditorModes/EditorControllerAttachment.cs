namespace ClassicTilestorm
{
	public class EditorControllerAttachment : EditorControllerMovement
	{
		private const EditorMarkerUtil.MarkerType MarkerType = EditorMarkerUtil.MarkerType.Attachment;

		public EditorControllerAttachment(EditorController controller) : base(controller) { }

		public override bool IsMouseOverGUI() => base.IsMouseOverGUI() || AttachmentEditing.sidePanel.IsMouseOver;

		public override void OnMapLoaded()
		{
			AttachmentEditing.ResetInputState();
			AttachmentEditing.RebuildMarkers(iMapManager, MarkerType);
		}

		public override void OnEnable()
		{
			base.OnEnable();
			AttachmentEditing.OnEnableShared(iMapManager, MarkerType);
		}

		public override void OnDisable()
		{
			base.OnDisable();
			AttachmentEditing.OnDisableShared();
		}

		public override void Update()
		{
			base.Update();
			AttachmentEditing.Update(camera, iMapManager, MarkerType, IsMouseOverGUI());
		}

		public override void OnGUI()
		{
			AttachmentEditing.OnGUI(iMapManager, camera, MarkerType);
		}
	}
}