using UnityEngine;

namespace MassiveHadronLtd
{
	public static class ApplicationQuit
	{
		public static bool IsQuitting { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Initialize()
		{
			IsQuitting = false;
			Application.quitting += OnQuitting;
		}

		private static void OnQuitting()
		{
			IsQuitting = true;
		}
	}
}