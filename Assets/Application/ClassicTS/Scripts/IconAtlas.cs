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
		private readonly Color _backgroundColor;

		// Optional: you can expose this if you ever want per-atlas light customization
		private readonly Vector3 _fixedLightDirection = new Vector3(0.5f, 1.0f, -0.8f).normalized;

		public IconAtlas(
			int cellSize,
			int columns,
			IEnumerable<Definition> filteredDefs,
			bool includeGround = false,
			Color? background = null,
			float yaw = 35f,
			float pitch = 30f)
		{
			_includeGround = includeGround;
			_yaw = yaw;
			_pitch = pitch;
			_backgroundColor = background ?? new Color(0, 0, 0, 0); // default transparent

			Initialize(
				cellSize: cellSize,
				columns: columns,
				itemsToRender: filteredDefs ?? Enumerable.Empty<Definition>(),
				background: _backgroundColor);
		}

		protected override IDisposable CreateRenderer(int cellSize, Color background)
		{
			// We ignore the passed background here and use our stored one
			// (GridAtlas might pass it, but we want consistent transparent bg most times)
			return new ReusableIconRenderer(
				size: cellSize,
				background: _backgroundColor,
				includeGround: _includeGround,
				initialYaw: _yaw,
				initialPitch: _pitch);
		}

		protected override Texture2D GenerateIcon(IDisposable renderer, object item, int index)
		{
			if (item is not Definition def) return null;

			var r = (ReusableIconRenderer)renderer;

			// Optional: override light direction to fixed world-space (recommended for atlas consistency)
			// This requires a small change in ReusableIconRenderer — see note below
			// For now we keep camera-relative as in your working version

			return r.RenderIcon(def, yaw: _yaw, pitch: _pitch);
		}

		// Optional helper if you want to regenerate the atlas later
		public void Refresh(IEnumerable<Definition> newFilteredDefs)
		{
			// If MassiveHadronLtd.GridAtlas has a way to rebuild, call it
			// Otherwise dispose & recreate the atlas instance externally
			// This method is just a placeholder — implement according to your needs
			Debug.Log($"IconAtlas refresh requested for {newFilteredDefs.Count()} definitions");
		}
	}
}