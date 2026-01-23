using System;
using UnityEngine;

namespace ClassicTilestorm
{
	public class MapManager : MonoBehaviour
	{
		public Map CurrentMap { get; set; }

		public Transform CurrentTransform => transform;

		private static MapManager instance;

		public static Quaternion LocalRotation(int tileIndex, Quaternion worldRotation) => worldRotation;
		public static Quaternion WorldRotation(int tileIndex, Quaternion localRotation) => localRotation;

		public static Vector3 LocalPosition(int tileIndex, Vector3 worldPosition)
			=> instance == null || tileIndex < 0 ? worldPosition : worldPosition - instance.CurrentMap.TileWorldPosition(tileIndex);

		public static Vector3 WorldPosition(int tileIndex, Vector3 localPosition)
			=> instance == null || tileIndex < 0 ? localPosition : localPosition + instance.CurrentMap.TileWorldPosition(tileIndex);

		private void OnDestroy()
		{
			CurrentMap?.CleanupAttachmentInstances();
			CurrentMap?.DestroyAllTiles();
			if (ReferenceEquals(MapAttachmentExtensions.CurrentMapManager, this))
				MapAttachmentExtensions.ClearActiveMapManager();
			instance = null;
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
			manager.CurrentMap = map ?? throw new ArgumentNullException(nameof(map));
			map.Initialise();
			MapAttachmentExtensions.SetActiveMapManager(map);
			instance = manager;
			return manager;
		}
	}
}