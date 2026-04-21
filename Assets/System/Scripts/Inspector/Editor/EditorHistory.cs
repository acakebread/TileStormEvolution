using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using System.Linq;
using static UnityEditor.GlobalObjectId;

public static class EditorHistoryButtons
{
    const string MenuPath = "Navigation/Selection History";
    static bool Starting = true;

    static EditorHistoryButtons()
    {
        Starting = true;
        Debug.Log("Start");
        Data.Load();
        Data.Add(Selection.objects);
        Selection.selectionChanged -= OnSelectionChanged;
        Selection.selectionChanged += OnSelectionChanged;
    }

    static void OnSelectionChanged() => Data.Add(Selection.objects);

    #region Buttons
    [MainToolbarElement(MenuPath, defaultDockPosition = MainToolbarDockPosition.Left)]
    static IEnumerable<MainToolbarElement> NavigationButtons()
    {
        yield return new MainToolbarButton(new(null, LeftArrow, "History Back"), Data.Back) { enabled = Data.hasBack };
        yield return new MainToolbarButton(new($"{Data.index}/{Data.count}", null, "Get all selections"), Data.Inventory) { enabled = Data.count > 0 };
        yield return new MainToolbarButton(new(null, RightArrow, "History Forward"), Data.Forward) { enabled = Data.hasForward };
        Starting = false;
    }
    #endregion

    static void Refresh() { if (!Starting) MainToolbar.Refresh(MenuPath); }

    #region Resources
    static Texture2D LeftArrow => Resources.Load<Texture2D>("arrow-left");
    static Texture2D RightArrow => Resources.Load<Texture2D>("arrow-right");
    #endregion

    #region Data
    static readonly HistoryData Data = new();

    [System.Serializable]
    class HistoryData
    {
        const string PrefKey = "EditorHistory";
        [SerializeField] List<Entry> entries = new();
        public int index;
        public int count => entries.Count;
        public bool oob => index < 0 || index >= count;
        public void Clear() { index = -1; entries.Clear(); }

        #region └Navigation
        public bool hasBack => index > 0;
        public void Back() => Select(--index);
        public bool hasForward => index < count - 1;
        public void Forward() => Select(++index);
        public void Inventory()
        {
            for (int i = 0; i < count; i++)
            {
                var objects = Get(i);
                Debug.Log($"{i}: {string.Join("'", objects.Where(o => o).Select(o => $"'{o}'"))}");
            }
        }
		public void Select(int index)
		{
			if (index < 0 || index >= count) return;   // basic bounds check

			this.index = index;

			Object[] objects = Get(index);

			// Only act if we actually resolved something
			if (objects != null && objects.Length > 0)
			{
				Selection.objects = objects;

				// Ping the first one (safe now)
				EditorGUIUtility.PingObject(objects[0]);

				if (SceneView.lastActiveSceneView != null)
					SceneView.lastActiveSceneView.FrameSelected();

				Save();
				Refresh();
			}
			else
			{
				// Optional: clean up bad entry so it doesn't keep failing
				// entries.RemoveAt(index);
				// if (this.index >= entries.Count) this.index = entries.Count - 1;
				// Save();
				// Refresh();

				Debug.LogWarning($"History entry {index} resolved to no valid objects (possibly deleted or temporary object).");
			}
		}

		Object[] Get(int index) => entries[index].elements
            .Select(s => !string.IsNullOrEmpty(s) && GlobalObjectId.TryParse(s, out var gid)
            ? GlobalObjectIdentifierToObjectSlow(gid) : null).Where(o => o).ToArray();
        #endregion

        #region └Add
        public void Add(Object[] objects)
        {
            Entry entry = new() { elements = objects.Where(o => o).Select(o => $"{GetGlobalObjectIdSlow(o)}").ToArray() };
            if (!oob && entries[index].elements.SequenceEqual(entry.elements)) return;

            Entry[] current = entries.Take(index + 1).Append(entry).ToArray();

            Clear();
            entries.AddRange(current);
            index = count - 1;

            Save();
            Refresh();
        }
        #endregion

        #region └Persistence
        public void Save() => EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(this));
        public void Load()
        {
            Clear();
            if (!EditorPrefs.HasKey(PrefKey)) return;
            Debug.Log(EditorPrefs.GetString(PrefKey));
            JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(PrefKey), this);
        }
        #endregion

        [System.Serializable]
        class Entry { public string[] elements; }
    }
    #endregion
}
