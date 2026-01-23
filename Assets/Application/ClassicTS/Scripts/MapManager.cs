using System;
using UnityEngine;

namespace ClassicTilestorm
{
	public class MapManager : MonoBehaviour
	{
		private void OnDestroy()
		{
			MainController.CurrentMap?.Destroy();
			if (ReferenceEquals(MapAttachmentExtensions.CurrentMap, this))
				MapAttachmentExtensions.ClearActiveMapManager();
		}

		public static MapManager Instantiate(Map map, Transform parent = null)
		{
			if (map == null || string.IsNullOrEmpty(map.name))
			{
				Debug.LogError("Cannot instantiate MapManager: invalid map or name.");
				return null;
			}

			var go = new GameObject($"Map: {map.name}");
			if (parent != null) go.transform.SetParent(parent, false);
			Map.parentTransform = go.transform;

			var manager = go.AddComponent<MapManager>();
			MainController.CurrentMap = map ?? throw new ArgumentNullException(nameof(map));
			map.Initialise();
			return manager;
		}
	}
}