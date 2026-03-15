using UnityEngine;

namespace ClassicTilestorm
{
	internal interface ISelectable
	{
		void OnSelect(IMapEdit map, Camera camera) { } // Default: do nothing
		void OnDeselect(IMapEdit iMap, Camera camera) { } // Default: do nothing
		void OnUpdate(IMapEdit map, Camera camera) { } // Default: do nothing
		bool OnGizmoInput(IMapEdit map, Camera camera) => false; // Default: return false
	}

	internal interface ITransformableAttachment
	{
		Vector3 Position { get; }
		Quaternion Rotation { get; }
	}
}