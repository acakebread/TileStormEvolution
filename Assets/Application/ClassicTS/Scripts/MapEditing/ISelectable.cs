using UnityEngine;

namespace ClassicTilestorm
{
	public interface ISelectable
	{
		void OnSelect(IMapEdit map, Camera camera) { } // Default: do nothing
		void OnDeselect(IMapEdit iMap, Camera camera) { } // Default: do nothing
		void OnUpdate(IMapEdit map, Camera camera) { } // Default: do nothing
		bool OnGizmoInput(IMapEdit map, Camera camera) => false; // Default: return false
	}
}