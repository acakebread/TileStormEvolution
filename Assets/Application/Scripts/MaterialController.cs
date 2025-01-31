// Copyright 2022 massivehadron.com ltd. created 05/10/2022 by Andrew Cakebread

using System.Collections;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class MaterialController : MonoBehaviour
	{
		public Material opaque;
		public Material transparent;

		void Awake()
		{
			opaque = opaque.clone();
			transparent = transparent.clone();
		}

		public void OnEnable()
		{
			GetComponent<Renderer>().material = transparent;
			StartCoroutine(animate());
			//local function
			IEnumerator animate()
			{
				float t = 0;
				while (t<0.5f)
				{
					var color = GetComponent<Renderer>().material.color;
					color.a = t * 2;
					GetComponent<Renderer>().material.color = color;
					t += Time.deltaTime;
					yield return null;
				}
				GetComponent<Renderer>().material = opaque;
			}
		}
	}
}