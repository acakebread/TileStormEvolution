using UnityEngine;

public class PreviewSettings : MonoBehaviour
{
	[Header("Workaround for inverted .obj meshes")]
	[SerializeField] private bool flip = true;
	public static bool Flip => instance.flip;

	[Header("load map scrambled or solved")]
	[SerializeField] private bool scramble = true;
	public static bool Scramble => instance.scramble;

	[Header("resource paths")]
	[SerializeField] private string geometryPath = "Geometry/obj/";
	public static string GeometryPath => instance.geometryPath;

	[SerializeField] private string texturePath = "Textures/";
	public static string TexturePath => instance.texturePath;

	public static PreviewSettings instance;
    void Awake() => instance = this;
}
