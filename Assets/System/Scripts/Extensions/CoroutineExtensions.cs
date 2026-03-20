using UnityEngine;
using System.Collections;

namespace MassiveHadronLtd
{
	public static class CoroutineExtensions
	{
		public static IEnumerator WaitFrames(this MonoBehaviour behaviour, int count)
		{
			for (int i = 0; i < count; i++) yield return null;
		}
	}
}