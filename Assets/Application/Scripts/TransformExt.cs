// Copyright 2020 massivehadron.com ltd. created 24/07/2020 by Andrew Cakebread

using UnityEngine;

namespace MassiveHadronLtd
{
	public static class TransformExt
	{
		public static void SetLayer(this Transform src, int layer)
		{
			src.gameObject.layer = layer;
			foreach (Transform child in src) { child.SetLayer(layer); }
		}

		public static void SetRenderersEnabled(this Transform src, bool state)
		{
			foreach (MeshRenderer renderer in src.GetComponentsInChildren(typeof(MeshRenderer))) { renderer.enabled = state; }
			foreach (SpriteRenderer renderer in src.GetComponentsInChildren(typeof(SpriteRenderer))) { renderer.enabled = state; }
		}
	}
}