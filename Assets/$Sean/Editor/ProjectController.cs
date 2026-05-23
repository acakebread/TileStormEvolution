#pragma warning disable UDR0001
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static pigbrain.core.Inspector;

namespace pigbrain.core.Project
{
    public class ProjectController : EditorWindow, IHasCustomMenu
    {
        Vector2 scroll;
        [MenuItem("Tools/pigbrain/Project Controller")]
        static void Open()
        {
            var window = GetWindow<ProjectController>();
            window.titleContent = new GUIContent($"{Application.productName}");
        }

        void OnEnable()
        {
            // FavouriteService.AddChangeListener(OnFavouritesChanged);
        }
        // void OnDisable() => FavouriteService.RemoveChangeListener(OnFavouritesChanged);
        void OnFavouritesChanged() => Repaint();

        public void AddItemsToMenu(GenericMenu menu) =>
            menu.AddItem(new GUIContent("Refresh"), false, () => Repaint());

        void Update()
        {
            if (Application.isPlaying)
                Repaint();
        }

        void OnGUI()
        {
            using (var scrollView = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = scrollView.scrollPosition;

                if (Application.isPlaying)
                {
                    // DrawDiagnostics();
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(8);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.Space(8);
                        ProjectBuiilder.DrawGUI();
                        DrawControlObjects();
                        DrawFavourites();
                    }
                    GUILayout.Space(8);
                }
            }
        }

        // #region Diagnostics

        // readonly int[] fps = new int[100];
        // int reportIndex;
        // void DrawDiagnostics()
        // {
        //     const float RowHeight = 80, ColumnWidth = 50;
        //     EditorGUILayout.Space(4);

        //     var report = RuntimePerformance.Instance.GetReport();
        //     int fpsIndex = reportIndex % fps.Length;
        //     fps[fpsIndex] = Mathf.RoundToInt(1 / report.frameTime);

        //     DrawHeader("Diagnostics:");
        //     using (new GUILayout.HorizontalScope(GUILayout.Height(RowHeight)))
        //     {
        //         float max = fps.Max();
        //         using (new GUILayout.VerticalScope(GUILayout.Width(ColumnWidth)))
        //         {
        //             GUILayout.Label($"{fps[fpsIndex]}", DiagnosticStyles.LargeNumber, GUILayout.Height(RowHeight / 2));
        //             GUILayout.Label($"{max}", DiagnosticStyles.LargeNumber, GUILayout.Height(RowHeight / 2));
        //         }
        //         using (new GUILayout.VerticalScope())
        //         {
        //             using (new GUILayout.HorizontalScope(GUILayout.Height(RowHeight)))
        //             {
        //                 Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(RowHeight));
        //                 Rect b = rect.WithW(Mathf.Max(3, Mathf.FloorToInt(rect.width / fps.Length)));
        //                 Rect r = b.AddW(-2);
        //                 for (int i = 0; i < fps.Length; i++)
        //                     EditorGUI.DrawRect(r.AddMinX(i * b.width).WithH(fps[i] / max * r.height), Color.red);
        //             }
        //         }
        //     }
        //     reportIndex++;
        // }
        // static class DiagnosticStyles
        // {
        //     public static readonly GUIStyle LargeNumber;
        //     static DiagnosticStyles()
        //     {
        //         LargeNumber = new(EditorStyles.boldLabel)
        //         {
        //             alignment = TextAnchor.MiddleCenter,
        //             fontSize = 20
        //         };
        //     }
        // }
        // #endregion

        #region Favourites
        void DrawFavourites()
        {
            EditorGUILayout.Space(4);
            DrawHeader("Favourites:");
            // if (FavouriteService.HasFavourites())
            // {
            //     foreach (var prefab in FavouriteService.Favourites.Where(p => p))
            //     {
            //         using (new GUILayout.HorizontalScope())
            //         {
            //             EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
            //             GameObjectHeader.DrawButton(prefab);
            //         }
            //     }
            // }
            // else GUILayout.Label("No favourites!", GUI.skin.button);
        }
        #endregion

        #region Control Objects
        void DrawControlObjects()
        {
            EditorGUILayout.Space(4);
            DrawHeader("Objects:");
            // var items = GetControlObjects();

            // Dictionary<Type, List<ObjectCache.Item>> grouped = new();
            // List<ObjectCache.Item> nonGrouped = new();

            // // group items marked as grouped
            // foreach (ObjectCache.Item item in items)
            // {
            //     if (item.attribute.group)
            //     {
            //         var type = item.target.GetType();
            //         if (!grouped.ContainsKey(type))
            //             grouped.Add(type, new());
            //         grouped[type].Add(item);
            //     }
            //     else nonGrouped.Add(item);
            // }

            // // move them into a single list
            // var itemGroups = grouped.Values.Concat(nonGrouped.Select(i => new List<ObjectCache.Item>() { i }))
            //     .OrderByDescending(g => g.First().attribute.priority)
            //     .ToArray();

            // foreach (List<ObjectCache.Item> group in itemGroups)
            // {
            //     using (new EditorGUILayout.HorizontalScope())
            //     {
            //         if (group.Count == 1)
            //         {
            //             var item = group[0];
            //             EditorGUILayout.ObjectField(item.target, item.target.GetType(), true);
            //             foreach (var b in item.buttons)
            //             {
            //                 using var ds = new EditorGUI.DisabledScope(!(b.validation == null || (bool)b.validation.Invoke(item.target, null)));
            //                 if (GUILayout.Button(b.name))
            //                 {
            //                     b.action.Invoke(item.target, null);
            //                     EditorUtility.SetDirty(item.target);
            //                 }
            //             }
            //         }
            //         else
            //         {
            //             var first = group[0];
            //             if (GUILayout.Button($"Selection ({first.target.GetType().Name})", EditorStyles.objectField))
            //                 Selection.objects = group.Select(g => g.target is ScriptableObject ? g.target : (g.target as Component).gameObject).ToArray();

            //             foreach (var b in first.buttons)
            //             {
            //                 bool disabled = false;

            //                 foreach (var item in group)
            //                 {
            //                     disabled = b.validation != null && (bool)b.validation.Invoke(item.target, null) == false;
            //                     if (disabled) break;
            //                 }

            //                 if (GUILayout.Button($"{b.name} ({group.Count})"))
            //                 {
            //                     foreach (var item in group)
            //                     {
            //                         b.action.Invoke(item.target, null);
            //                         EditorUtility.SetDirty(item.target);
            //                     }
            //                 }
            //             }
            //         }
            //     }

            //     EditorGUILayout.Space(2);
        }
    }

        #endregion

    // #region Get Objects
    // static ObjectCache.Item[] GetControlObjects() => ObjectCache.Get();

    // static class ObjectCache
    // {
    //     static Item[] Cache;
    //     static bool Dirty = true;

    //     internal class Item
    //     {
    //         public Object target;
    //         public ControlAttribute attribute;
    //         public readonly List<Button> buttons = new();
    //         public Item(Object target, ControlAttribute attribute)
    //         {
    //             this.target = target;
    //             this.attribute = attribute;
    //         }
    //         public static bool Equal(Item a, Item b) => a.GetType() == b.GetType();
    //     }

    //     internal class Button
    //     {
    //         public string name;
    //         public MethodInfo action;
    //         public MethodInfo validation;
    //         public Button(string name) => this.name = name;
    //         public int group;
    //     }

    //     static ObjectCache()
    //     {
    //         OnBeforeAssemblyReload();
    //         EditorApplication.projectChanged += MarkDirty;
    //         EditorApplication.hierarchyChanged += MarkDirty;
    //         AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
    //     }

    //     static void MarkDirty() => Dirty = true;

    //     static void OnBeforeAssemblyReload()
    //     {
    //         EditorApplication.projectChanged -= MarkDirty;
    //         EditorApplication.hierarchyChanged -= MarkDirty;
    //         AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
    //     }

    //     internal static Item[] Get()
    //     {
    //         // if (Application.isPlaying) return Cache;
    //         if (Dirty)
    //         {
    //             Dirty = false;
    //             Cache = Build().ToArray();

    //             foreach (var item in Cache)
    //             {
    //                 Dictionary<string, Button> buttons = new();
    //                 foreach (var method in item.target.GetType().GetMethods(ReflectionUtility.DefaultBindings))
    //                 {
    //                     ButtonAttribute attribute = method.GetCustomAttribute<ButtonAttribute>();
    //                     if (attribute == null) continue;

    //                     if (!buttons.TryGetValue(attribute.name, out Button button))
    //                     {
    //                         buttons.Add(attribute.name, button = new(attribute.name));
    //                         item.buttons.Add(button);
    //                     }
    //                     if (attribute.validation) button.validation = method;
    //                     else button.action = method;
    //                 }
    //             }
    //         }
    //         return Cache;
    //     }

    //     static bool TryGetAttribute(Object o, Filter filter, out ControlAttribute attribute)
    //     {
    //         attribute = o.GetType().GetCustomAttribute<ControlAttribute>();
    //         return attribute != null && attribute.filter == filter;
    //     }

    //     static IEnumerable<Item> Build()
    //     {
    //         var p = Profiler.Start();

    //         foreach (var o in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
    //         {
    //             if (!o || !o.gameObject.scene.IsValid()) continue;
    //             if (!TryGetAttribute(o, Filter.Hierarchy, out ControlAttribute attribute)) continue;
    //             yield return new Item(o, attribute);
    //         }

    //         foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject"))
    //         {
    //             var o = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
    //             if (!o || !TryGetAttribute(o, Filter.Project, out ControlAttribute attribute)) continue;
    //             yield return new Item(o, attribute);
    //         }

    //         foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
    //         {
    //             var o = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
    //             if (!o) continue;
    //             foreach (var c in o.GetComponents<MonoBehaviour>())
    //                 if (c && TryGetAttribute(c, Filter.Project, out ControlAttribute attribute))
    //                     yield return new Item(c, attribute);
    //         }

    //         Profiler.StopAndLog(p, "Project");
    //     }
    // }
    // #endregion
    // }
}
