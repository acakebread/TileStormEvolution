using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class QuadStrip
	{
		public List<int> indexBlocks;
		public List<int> vertexBlocks;
	}

	public class QuadStripAllocator
	{
		private class DynamicAllocator
		{
			public const int DefaultMaxBlocks = 1024;
			public int MaxBlocks { get; private set; } = DefaultMaxBlocks;
			public void SetMaxBlocks(int newMax) => MaxBlocks = newMax < 1 ? 1 : newMax;

			private readonly List<int> _freeBlocks = new();
			private int _nextBlockId = 0;

			public int Allocate()
			{
				if (_freeBlocks.Count > 0)
				{
					int id = _freeBlocks[_freeBlocks.Count - 1];
					_freeBlocks.RemoveAt(_freeBlocks.Count - 1);
					return id;
				}

				if (_nextBlockId >= MaxBlocks) return -1;
				return _nextBlockId++;
			}

			public bool Release(int blockId)
			{
				if (blockId < 0 || blockId >= _nextBlockId) return false;
				if (_freeBlocks.Contains(blockId)) return false;

				_freeBlocks.Add(blockId);

				return true;
			}

			public void Defrag()
			{
				while (_nextBlockId > 0 && _freeBlocks.Contains(_nextBlockId - 1))
				{
					_freeBlocks.Remove(_nextBlockId - 1);
					_nextBlockId--;
				}
			}

			public void Clear()
			{
				_freeBlocks.Clear();
				_nextBlockId = 0;
			}

			public int AvailableBlockCount => _freeBlocks.Count - _nextBlockId + MaxBlocks;
			public int AllocatedBlockCount => _nextBlockId - _freeBlocks.Count;

			// Debug section
			public int HighWaterMark => _nextBlockId;
		}

		public const int IndicesPerBlock = 6;
		private readonly DynamicAllocator _indexBlockAllocator = new();
		public int MaxIndexBlocks => _indexBlockAllocator.MaxBlocks;

		public const int VerticesPerBlock = 2;
		private readonly DynamicAllocator _vertexBlockAllocator = new();
		public int MaxVertexBlocks => _vertexBlockAllocator.MaxBlocks;

		private readonly List<int> _indices = new();
		private readonly List<Vector3> _vertices = new();
		private readonly List<Color> _colors = new();
		private readonly List<Vector2> _uv = new();

		private readonly List<QuadStrip> activeStrips = new();

		public IReadOnlyList<int> Indices => _indices;
		public IReadOnlyList<Vector3> Vertices => _vertices;
		public IReadOnlyList<Color> Colors => _colors;
		public IReadOnlyList<Vector2> UV => _uv;

		internal List<int> MutableIndices => _indices;
		internal List<Vector3> MutableVertices => _vertices;
		internal List<Color> MutableColors => _colors;
		internal List<Vector2> MutableUV => _uv;

		public void SetMaxIndexBlocks(int newMax) => _indexBlockAllocator.SetMaxBlocks(newMax);
		public void SetMaxVertexBlocks(int newMax) => _vertexBlockAllocator.SetMaxBlocks(newMax);

		private void EnsureIndexCapacity(int required)
		{
			while (_indices.Count < required) _indices.Add(0);
		}

		private void EnsureVertexCapacity(int required)
		{
			while (_vertices.Count < required) _vertices.Add(Vector3.zero);
			while (_colors.Count < required) _colors.Add(Color.white);
			while (_uv.Count < required) _uv.Add(Vector2.zero);
		}

		public QuadStrip AllocateStrip(int numQuads)
		{
			if (numQuads < 1) return null;
			if (_indexBlockAllocator.AvailableBlockCount < numQuads) return null;
			if (_vertexBlockAllocator.AvailableBlockCount < numQuads + 1) return null;

			var strip = new QuadStrip
			{
				indexBlocks = new List<int>(numQuads),
				vertexBlocks = new List<int>(numQuads + 1)
			};

			int maxIndexBlock = -1;
			for (int i = 0; i < numQuads; i++)
			{
				int idx = _indexBlockAllocator.Allocate(); // Guaranteed success
				strip.indexBlocks.Add(idx);
				if (idx > maxIndexBlock) maxIndexBlock = idx;
			}

			int maxVertexBlock = -1;
			for (int i = 0; i < numQuads + 1; i++)
			{
				int vtx = _vertexBlockAllocator.Allocate(); // Guaranteed success
				strip.vertexBlocks.Add(vtx);
				if (vtx > maxVertexBlock) maxVertexBlock = vtx;
			}

			// One-time capacity
			EnsureIndexCapacity((maxIndexBlock + 1) * IndicesPerBlock);
			EnsureVertexCapacity((maxVertexBlock + 1) * VerticesPerBlock);

			// Fill indices
			for (int q = 0; q < numQuads; q++)
			{
				int idxBase = strip.indexBlocks[q] * IndicesPerBlock;
				int v0 = strip.vertexBlocks[q + 0] * VerticesPerBlock;
				int v1 = v0 + 1;
				int v2 = strip.vertexBlocks[q + 1] * VerticesPerBlock;
				int v3 = v2 + 1;

				_indices[idxBase + 0] = v0;
				_indices[idxBase + 1] = v2;
				_indices[idxBase + 2] = v1;
				_indices[idxBase + 3] = v1;
				_indices[idxBase + 4] = v2;
				_indices[idxBase + 5] = v3;
			}

			activeStrips.Add(strip);
			return strip;
		}

		public bool ReleaseStrip(QuadStrip strip)
		{
			if (strip == null || !activeStrips.Contains(strip)) return false;

			foreach (int idxBlock in strip.indexBlocks)
			{
				int idxBase = idxBlock * IndicesPerBlock;
				for (int j = 0; j < IndicesPerBlock; j++)
					_indices[idxBase + j] = 0;
			}

			// Release blocks
			foreach (int idx in strip.indexBlocks) _indexBlockAllocator.Release(idx);
			foreach (int vtx in strip.vertexBlocks) _vertexBlockAllocator.Release(vtx);

			bool removed = activeStrips.Remove(strip);
			if (activeStrips.Count == 0)
			{
				_indexBlockAllocator.Clear();
				_vertexBlockAllocator.Clear();
			}
			return removed;
		}

		public void Defrag()
		{
			_indexBlockAllocator.Defrag();
			_vertexBlockAllocator.Defrag();
		}

		// Debug section
		public int IndexBlockAllocated => _indexBlockAllocator.AllocatedBlockCount;
		public int VertexBlockAllocated => _vertexBlockAllocator.AllocatedBlockCount;
		public int IndexHighWater => _indexBlockAllocator.HighWaterMark;
		public int VertexHighWater => _vertexBlockAllocator.HighWaterMark;
	}
}