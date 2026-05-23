using System;
using UnityEngine;

namespace ClassicTilestorm
{
	[CreateAssetMenu(fileName = "ContentRootSettings", menuName = "ClassicTilestorm/Content Root Settings")]
	public sealed class ContentRootSettingsAsset : ScriptableObject
	{
		[SerializeField] private bool automaticallyAddRoots = true;
		[SerializeField] private string[] contentRoots = Array.Empty<string>();

		public bool AutomaticallyAddRoots
		{
			get => automaticallyAddRoots;
			set => automaticallyAddRoots = value;
		}

		public string[] ContentRoots
		{
			get => contentRoots ?? Array.Empty<string>();
			set => contentRoots = value ?? Array.Empty<string>();
		}
	}
}
