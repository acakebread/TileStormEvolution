#pragma warning disable UDR0001
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace pigbrain.core.project.editor
{
    public class ProjectSettings : ScriptableObject
    {
        static ProjectSettings AssetInstance;
        public static ProjectSettings Instance => TryGetAsset();

        const string EditorPath = "Assets/Editor/ProjectSettings.asset";

        [SerializeField] Project.ProjectBuiilder.BuildButton[] buildButtons;
        public static Project.ProjectBuiilder.BuildButton[] GetBuildButtons()
        {
            Instance.buildButtons ??= new Project.ProjectBuiilder.BuildButton[0];
            return Instance.buildButtons;
        }

        // #region Asset
        // public void Write()
        // {
        //     EditorUtility.SetDirty(this);
        //     AssetDatabase.SaveAssetIfDirty(this);
        // }

        static ProjectSettings TryGetAsset()
        {
            if (!AssetInstance) AssetInstance = AssetDatabase.LoadAssetAtPath<ProjectSettings>(EditorPath);
            if (!AssetInstance)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(EditorPath));
                AssetInstance = CreateInstance<ProjectSettings>();
                AssetDatabase.CreateAsset(AssetInstance, EditorPath);
                AssetDatabase.SaveAssets();
            }
            return AssetInstance;
        }
        // #endregion
    }
}
