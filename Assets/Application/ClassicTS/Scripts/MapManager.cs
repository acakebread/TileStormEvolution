using System;
using UnityEngine;

namespace ClassicTilestorm
{
	public interface IMapData
	{
		int Width { get; }
		int Height { get; }
		int Count { get; }
		int[] Indices { get; }
	}

	public interface IMapManager : IMapData
	{
		Transform CurrentTransform { get; }
		Map CurrentMap { get; }
	}

	public class MapManager : MonoBehaviour, IMapManager
	{
		private Map currentMap;

		public Map CurrentMap => currentMap;
		public Transform CurrentTransform => transform;

		public int Width => currentMap?.width ?? 0;
		public int Height => currentMap?.height ?? 0;
		public int Count => Width * Height;
		public int[] Indices => currentMap?.indices;

		private static MapManager instance;

		public static Quaternion LocalRotation(int tileIndex, Quaternion worldRotation) => worldRotation;
		public static Quaternion WorldRotation(int tileIndex, Quaternion localRotation) => localRotation;

		public static Vector3 LocalPosition(int tileIndex, Vector3 worldPosition)
			=> instance == null || tileIndex < 0 ? worldPosition : worldPosition - instance.currentMap.TileWorldPosition(tileIndex);

		public static Vector3 WorldPosition(int tileIndex, Vector3 localPosition)
			=> instance == null || tileIndex < 0 ? localPosition : localPosition + instance.currentMap.TileWorldPosition(tileIndex);


		private void OnDestroy()
		{
			currentMap?.CleanupAttachmentInstances();
			currentMap?.DestroyRuntimeTiles();
			if (ReferenceEquals(MapAttachmentExtensions.CurrentMapManager, this))
				MapAttachmentExtensions.ClearActiveMapManager();
			instance = null;
		}

		private void Initialise(Map map)
		{
			currentMap = map ?? throw new ArgumentNullException(nameof(map));
			map.Initialise();
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
			MapAttachmentExtensions.SetActiveMapManager(manager);
			manager.Initialise(map);
			instance = manager;
			return manager;
		}
	}
}