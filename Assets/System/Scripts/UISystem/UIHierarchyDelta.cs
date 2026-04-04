using UnityEngine;
using System;
using System.Collections.Generic;

namespace MassiveHadronLtd.UI
{
	public class UIHierarchyDelta : MonoBehaviour
	{
		public event Action<GameObject> OnItemAdded;
		public event Action<GameObject> OnItemRemoved;
		public event Action OnItemsChanged;

		private readonly HashSet<Transform> knownChildren = new HashSet<Transform>();
		private bool isDirty = true;

		private void OnEnable()
		{
			isDirty = true;
		}

		private void LateUpdate()
		{
			if (!isDirty && transform.childCount == knownChildren.Count)
				return;

			isDirty = false;
			DetectChanges();
		}

		private void DetectChanges()
		{
			var currentChildren = new HashSet<Transform>();

			foreach (Transform child in transform)
			{
				currentChildren.Add(child);

				if (!knownChildren.Contains(child))
				{
					knownChildren.Add(child);
					OnItemAdded?.Invoke(child.gameObject);
					OnItemsChanged?.Invoke();
				}
			}

			// Check for removals
			var toRemove = new List<Transform>();
			foreach (var known in knownChildren)
			{
				if (!currentChildren.Contains(known))
				{
					toRemove.Add(known);
					OnItemRemoved?.Invoke(known.gameObject);
					OnItemsChanged?.Invoke();
				}
			}

			foreach (var item in toRemove)
				knownChildren.Remove(item);
		}

		// Force a full rescan (useful after RefreshMapList)
		public void ForceRescan()
		{
			isDirty = true;
		}
	}
}