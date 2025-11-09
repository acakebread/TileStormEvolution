// DynamicAllocator.cs
using System.Collections.Generic;

public class DynamicAllocator
{
	public const int DefaultMaxBlocks = 1024;

	// THIS IS THE SINGLE SOURCE OF TRUTH
	public int MaxBlocks { get; private set; } = DefaultMaxBlocks;

	private readonly List<int> _freeBlocks = new();
	private int _nextBlockId = 0;

	public DynamicAllocator()
	{
	}

	public DynamicAllocator(int maxBlocks)
	{
		SetMaxBlocks(maxBlocks);
	}

	public void SetMaxBlocks(int newMax)
	{
		if (newMax < 1) newMax = 1;
		MaxBlocks = newMax;
	}

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

		while (_nextBlockId > 0 && _freeBlocks.Contains(_nextBlockId - 1))
		{
			_freeBlocks.Remove(_nextBlockId - 1);
			_nextBlockId--;
		}

		return true;
	}

	public void Clear()
	{
		_freeBlocks.Clear();
		_nextBlockId = 0;
	}

	public int AvailableBlockCount => _freeBlocks.Count + (MaxBlocks - _nextBlockId);
	public int AllocatedBlockCount => _nextBlockId - _freeBlocks.Count;

	// Optional: expose high-water mark for debugging
	public int HighWaterMark => _nextBlockId;
}