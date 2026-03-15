using UnityEngine;

namespace ClassicTilestorm
{
	internal interface ISelectable
	{
		void OnSelect(EditorController controller) { }
		void OnDeselect(EditorController controller) { }
		void OnUpdate(EditorController controller) { }
		bool OnGizmoInput(EditorController controller) => false;
	}

	internal interface ITransformableAttachment
	{
		Vector3 Position { get; }
		Quaternion Rotation { get; }
	}
}