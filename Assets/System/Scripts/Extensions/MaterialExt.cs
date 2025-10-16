// Copyright 2018 massivehadron.com ltd. created 29/05/2018 by Andrew Cakebread

using UnityEngine;

namespace MassiveHadronLtd
{
	public static class MaterialExt
	{
		public static Material clone(this Material material) { return new Material(material) { name = material.name }; }
	}
}