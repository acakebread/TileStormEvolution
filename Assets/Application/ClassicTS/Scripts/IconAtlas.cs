using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	[Serializable]
	public class IconAtlas : MassiveHadronLtd.GridAtlas
	{
		private readonly bool _includeGround;
		private readonly float _yaw;
		private readonly float _pitch;

		public IconAtlas(
			int cellSize,
			int columns,
			IEnumerable<Definition> filteredDefs,
			bool includeGround = false,
			Color? background = null,
			float yaw = 35f,
			float pitch = 30f)
		{
			// 1. Set derived-specific fields FIRST
			_includeGround = includeGround;
			_yaw = yaw;
			_pitch = pitch;

			// 2. Now trigger atlas building — CreateRenderer will see correct values
			Initialize(
				cellSize: cellSize,
				columns: columns,
				itemsToRender: filteredDefs ?? Enumerable.Empty<Definition>(),
				background: background);
		}

		protected override IDisposable CreateRenderer(int cellSize, Color background)
		{
			return new ReusableIconRenderer(
				size: cellSize,
				background: background,
				includeGround: _includeGround,
				initialYaw: _yaw,
				initialPitch: _pitch);
		}

		protected override Texture2D GenerateIcon(IDisposable renderer, object item, int index)
		{
			if (item is not Definition def) return null;
			var r = (ReusableIconRenderer)renderer;
			return r.RenderIcon(def, yaw: _yaw, pitch: _pitch);
		}
	}
}
