using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[Serializable]
	public struct Variant
	{
		public HashId hash;
		public Vector3 delta;           // local position offset (usually small x/z values)
		public float angle;             // degrees, usually 0/90/180/270

		// ─── constructors (unchanged) ────────────────────────────────────────
		public Variant(HashId h) : this(h, Vector3.zero, 0f) { }
		public Variant(HashId h, Vector3 offset, float rotationDegrees)
		{
			hash = h;
			delta = offset;
			angle = rotationDegrees;
		}

		public static implicit operator HashId(Variant v) => v.hash;

		public readonly Definition definition => ResourceManager.GetDefinition(hash);

		public readonly bool IsDefaultEquivalent => definition != null && definition.IsDefaultEquivalent();
		public readonly bool HasNav => definition != null && definition.Nav != 0;
	}
}