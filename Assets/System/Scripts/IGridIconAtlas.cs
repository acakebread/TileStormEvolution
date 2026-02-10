// File: MassiveHadronLtd / IGridIconAtlas.cs
using UnityEngine;

namespace MassiveHadronLtd
{
	public interface IGridIconAtlas
	{
		Texture2D Texture { get; }
		int CellSize { get; }
		int Columns { get; }
		int Rows { get; }

		// If you need to highlight / pick icons later:
		// bool TryGetUVRect(int index, out Rect uvRect);
		// or bool TryGetUVRect(object key, out Rect uvRect);  // if you use HashId or something
	}
}