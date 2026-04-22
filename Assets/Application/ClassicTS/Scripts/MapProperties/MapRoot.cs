using UnityEngine;

namespace ClassicTilestorm
{
	/// <summary>
	/// Lightweight MonoBehaviour attached to the Map's root transform.
	/// Allows any child tile to quickly find its owning Map without statics or heavy traversal.
	/// </summary>
	[DisallowMultipleComponent]
	public class MapRoot : MonoBehaviour
	{
		public Map Map { get; private set; }

		public void Initialise(Map map)
		{
			if (map == null) throw new System.ArgumentNullException(nameof(map));
			Map = map;
		}

		private void OnDestroy()
		{
			Map = null;
		}
	}
}