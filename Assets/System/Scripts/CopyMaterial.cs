// Copyright 2022 massivehadron.com ltd. created 05/10/2022 by Andrew Cakebread

using UnityEngine;

namespace MassiveHadronLtd
{
	public class CopyMaterial : MonoBehaviour
	{
		void Awake() => GetComponent<Renderer>().material = GetComponent<Renderer>().material.clone();
	}
}