// Copyright 2022 massivehadron.com ltd. created 05/10/2022 by Andrew Cakebread

using UnityEngine;

namespace MassiveHadronLtd
{
	public class TileNameInit : MonoBehaviour
	{
		void Start() => GetComponent<TMPro.TMP_Text>().text = FindObjectOfType<MaterialController>(true).opaque.mainTexture.name;
	}
}