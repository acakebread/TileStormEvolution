#pragma warning disable UDR0001
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace pigbrain.core.Project
{
    public class ProjectController : EditorWindow
    {
        Vector2 scroll;
        [MenuItem("Tools/Project Controller")]
        static void Open()
        {
            var window = GetWindow<ProjectController>();
            window.titleContent = new GUIContent($"{Application.productName}");
        }

        // public void AddItemsToMenu(GenericMenu menu) =>
        //     menu.AddItem(new GUIContent("Refresh"), false, () => Repaint());

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
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(8);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.Space(8);
                        ProjectBuilder.DrawGUI();
                    }
                    GUILayout.Space(8);
                }
            }
        }

    }
}