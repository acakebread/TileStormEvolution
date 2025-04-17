// Copyright 2022 massivehadron.com ltd. created 01/11/2022 by Andrew Cakebread

using System.Collections;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class HideFrameZero : MonoBehaviour
	{
		void OnEnable()
		{
			transform.SetLayer(LayerMask.NameToLayer("Hidden"));
			StartCoroutine(show());
			//local function
			IEnumerator show()
			{
				yield return null;
				transform.SetLayer(LayerMask.NameToLayer("Default"));
			}
		}
	}
}