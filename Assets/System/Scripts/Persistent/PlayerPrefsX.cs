using UnityEngine;

namespace CrazyGames.Core.Persistent
{
	public static class PlayerPrefsX
	{
		public static void SetBool(string name, bool value) => PlayerPrefs.SetInt(name, value ? 1 : 0);
		public static bool GetBool(string name, bool defaultValue = false) => PlayerPrefs.GetInt(name, defaultValue ? 1 : 0) == 1;
	}
}