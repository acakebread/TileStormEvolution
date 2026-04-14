using UnityEngine;

namespace MassiveHadronLtd
{
	public static class PlayerPrefsX
	{
		private static void Save(bool value) { if (value) PlayerPrefs.Save(); }

		public static void SetBool(string name, bool value) => PlayerPrefs.SetInt(name, value ? 1 : 0);
		public static void SetBool(string name, bool value, bool save = false) { SetInt(name, value ? 1 : 0); Save(save); }
		public static bool GetBool(string name, bool defaultValue = false) => PlayerPrefs.GetInt(name, defaultValue ? 1 : 0) == 1;

		public static void SetInt(string name, int value) => PlayerPrefs.SetInt(name, value);
		public static void SetInt(string name, int value, bool save = false) { SetInt(name, value); Save(save); }
		public static int GetInt(string name, int defaultValue = 0) => PlayerPrefs.GetInt(name, defaultValue);

		public static void SetFloat(string name, float value) => PlayerPrefs.SetFloat(name, value);
		public static void SetFloat(string name, float value, bool save = false) { SetFloat(name, value); if (save) PlayerPrefs.Save(); }
		public static float GetFloat(string name, float defaultValue = 0f) => PlayerPrefs.GetFloat(name, defaultValue);

		public static void SetString(string name, string value) => PlayerPrefs.SetString(name, value);
		public static void SetString(string name, string value, bool save = false) { SetString(name, value); Save(save); }
		public static string GetString(string name, string defaultValue = "") => PlayerPrefs.GetString(name, defaultValue);

		public static bool HasKey(string value) => PlayerPrefs.HasKey(value);
	}
}