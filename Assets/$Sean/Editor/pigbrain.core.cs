using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace pigbrain.core
{
    namespace pigbrain.core.Generated
    {
        public static class Constants
        {
            public const string Namespace = "pigbrain.generated";
            public const string Path = "Assets/pigbrain.generated";
        }
    }

    public static class Inspector
    {
        public static float HeaderHeight => LineHeight + 4;
        public static float FullHeaderHeight => HeaderHeight + VerticalSpacing;

        public static float LineHeight => EditorGUIUtility.singleLineHeight;
        public static float FullLineHeight => LineHeight + VerticalSpacing;

        public static float VerticalSpacing => EditorGUIUtility.standardVerticalSpacing;
        public static float MiniFieldWidth = 50;
        public static float TinyFieldWidth = 30;
        public static float IndentSize = 15;
        public static float FoldoutWidth = IndentSize;
        public static float TotalIndentSize => EditorGUI.indentLevel * IndentSize;
        public static float LabelWidth => EditorGUIUtility.labelWidth;
        public static float Padding = 2;
        public static float Spacing = 8;
        public static float Separation = 8;

        #region "Controls"
        public readonly static Color HeaderColor = new(0.2f, 0.4f, 0.8f, 0.5f);
        public static void DrawHeader(string name) =>
            DrawHeader(name, HeaderColor);

        public static void DrawHeader(string name, Color color)
        {
            var rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.DrawRect(rect, color);
            EditorGUI.LabelField(rect, name, EditorStyles.boldLabel);
            EditorGUILayout.Space(2);
        }

        public static void DrawHeader(Rect position, string label, Color color = default)
        {
            var p = new Rect(position.x - 100, position.y, position.width + 200, position.height);
            EditorGUI.DrawRect(p, color);
            EditorGUI.LabelField(position, label, EditorStyles.boldLabel);

        }
        #endregion


        #region GUI Style
        public static class Styles
        {
            static Dictionary<string, GUIStyle> CachedStyles = new();
            public static GUIStyle ToolbarDropDown
            {
                get
                {
                    if (CachedStyles.TryGetValue(nameof(ToolbarDropDown), out var style)) return style;
                    style = new GUIStyle(EditorStyles.iconButton);
                    style.normal.scaledBackgrounds = new Texture2D[0];
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontStyle = FontStyle.Bold;
                    return CachedStyles[nameof(ToolbarDropDown)] = style;
                }
            }

            public static GUIStyle GetColorButtonStyle(string id, GUIStyleState normal, GUIStyleState hover)
            {
                if (CachedStyles.TryGetValue(nameof(id), out var style)) return style;
                style = new(GUI.skin.button)
                { normal = normal, hover = hover, active = normal, focused = normal, };
                return CachedStyles[id] = style;
            }

            public static GUIStyle ColorButton(Color fg, Color bg) => GetColorButtonStyle(
                nameof(ColorButton) + "_" + fg.ToString() + "_" + bg.ToString(),
                new() { textColor = fg, background = Texture2DX.CreateColor(bg) },
                new() { textColor = fg, background = Texture2DX.CreateColor(bg * 1.2f) });

            public static GUIStyle BlackOnWhiteButton => GetColorButtonStyle(
                nameof(BlackOnWhiteButton),
                new() { textColor = Color.black, background = Texture2D.whiteTexture },
                new() { textColor = Color.black, background = Texture2DX.CreateColor(Color.white * 1.2f) });

            public static GUIStyle WhiteOnWhiteButton => GetColorButtonStyle(
                nameof(WhiteOnWhiteButton),
                new() { textColor = Color.white, background = Texture2D.whiteTexture },
                new() { textColor = Color.white, background = Texture2DX.CreateColor(Color.white * 1.2f) });
        }
        #endregion

    }

    public static class CoreSubset
    {
        public static bool IsNullOrEmpty<T>(this IList<T> self) => self == null || self.Count == 0;
        public static bool IsNullOrEmpty<T>(this HashSet<T> self) => self == null || self.Count == 0;

        public static string Message(this string message, Color color) =>
            $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{message}</color>";

        public static IEnumerable<T> ForEach<T>(this Array self, Action<T> action)
        {
            foreach (T item in self.Cast<T>()) action(item);
            return self.Cast<T>();
        }

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> self, Action<T> action)
        {
            foreach (T item in self) action(item);
            return self;
        }

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> self, Action<T, int> action)
        {
            int counter = 0;
            foreach (T item in self) action(item, counter++);
            return self;
        }

        public static IEnumerable<T> ForGroup<T>(this IEnumerable<T> self, int count, Action onEnter, Action onExit, Action<T> action)
        {
            int index = 0;
            foreach (T item in self)
            {
                if (index % count == 0) onEnter?.Invoke();
                action(item);
                if (++index % count == 0) onExit?.Invoke();
            }
            if (index % count != 0) onExit?.Invoke();
            return self;
        }
    }

    public static class MemoryProfiler
    {
        public static string FormatBytes(long b) => FormatBytes((ulong)b);
        public static string FormatBytes(ulong b, ulong bSize = 1024)
        {
            ulong kb = bSize, mb = kb * bSize, gb = mb * bSize;
            if (b >= gb) return $"{b / (double)gb:0.0} GB";
            if (b >= mb) return $"{b / (double)mb:0.0} MB";
            if (b >= kb) return $"{b / (double)kb:0.0} KB";
            return $"{b} B";
        }
    }

    public static class PathUtility
    {
        public static ulong GetDirectorySize(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;

            ulong size = 0;
            var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                try { size += (ulong)new FileInfo(f).Length; }
                catch { }
            }
            return size;
        }

    }

    public static class Texture2DX
    {
        #region Color Texture
        static readonly Dictionary<Color, Texture2D> Texture2DColor = new();
        public static Texture2D CreateColor(Color color)
        {
            if (Texture2DColor.TryGetValue(color, out Texture2D texture)) return texture;
            texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return Texture2DColor[color] = texture;
        }
        #endregion 
    }
}
