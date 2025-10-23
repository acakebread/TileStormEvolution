using UnityEngine;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public static class MaterialManager
	{
		private static Dictionary<string, Material> materials = new();
		public static Material Get(string name)
		{
			if (true == materials.ContainsKey(name)) return materials[name];
			string _path = $"{PreviewSettings.MaterialPath}{name}".Replace(".mat", "");
			materials[name] = Resources.Load<Material>(_path);
			if (null == materials[name]) Debug.LogWarning($"Material not found: {_path}");
			return materials[name];
		}
	}
}