using UnityEngine;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public static class GeometryManager
	{
		private static Dictionary<string, GameObject> prefabs = new();

		public static GameObject Get(string name)
		{
			if (true == prefabs.ContainsKey(name)) return prefabs[name];
			var geomPath = $"{PreviewSettings.GeometryPath}{name}".Replace(".x", "");
			prefabs[name] = Resources.Load<GameObject>(geomPath);//loads as a prefab
			return prefabs[name];
		}
	}
}