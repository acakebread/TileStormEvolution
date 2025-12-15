// Copyright 2025 massivehadron.com ltd. created 15/12/2022 by Andrew Cakebread


using System.Collections;
using UnityEngine;

public class HideFrameZero : MonoBehaviour
{
	private Renderer[] renderers;
	private bool[] wasEnabled;

	void OnEnable()
	{
		// Cache all renderers once
		if (renderers == null)
		{
			renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
			wasEnabled = new bool[renderers.Length];
		}

		// Save current enabled state and disable all
		for (int i = 0; i < renderers.Length; i++)
		{
			wasEnabled[i] = renderers[i].enabled;
			renderers[i].enabled = false;
		}

		StartCoroutine(RestoreNextFrame());
	}

	IEnumerator RestoreNextFrame()
	{
		yield return null;

		// Restore original enabled states
		for (int i = 0; i < renderers.Length; i++)
		{
			renderers[i].enabled = wasEnabled[i];
		}
	}
}

//using System.Collections;
//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public class HideFrameZero : MonoBehaviour
//	{
//		void OnEnable()
//		{
//			transform.SetLayer(LayerMask.NameToLayer("Hidden"));
//			StartCoroutine(show());
//			//local function
//			IEnumerator show()
//			{
//				yield return null;
//				transform.SetLayer(LayerMask.NameToLayer("Default"));
//			}
//		}
//	}
//}