using UnityEngine;

namespace ClassicTilestorm
{
	internal interface ISelectable
	{
		void Apply(EditorController controller) { }
		void Revert(EditorController controller) { }
		void Discard(EditorController controller) { }

		void Select(EditorController controller) { }
		void Deselect(EditorController controller) { }
		void Update(EditorController controller) { }
		bool OnGizmoInput(EditorController controller) => false;
	}

	internal interface ITransformableAttachment
	{
		Vector3 Position { get; }
		Quaternion Rotation { get; }
	}
}