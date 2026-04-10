using UnityEngine;

public class MeshConverter : MonoBehaviour
{
	public string fname = null;
    private string inputWWMPath = null;
	private string outputPath = null;

	void Start()
    {
		if (null == inputWWMPath) inputWWMPath = Application.streamingAssetsPath + "/ClassicTS/WWM/" + fname;
		var nullpath = null == outputPath;

		if (true == nullpath) outputPath = Application.persistentDataPath + "/" + fname.Replace(".wwm", ".obj");
		WWMToOBJConverter.ConvertWWMToOBJ(inputWWMPath, outputPath);

		if (true == nullpath) outputPath = Application.persistentDataPath + "/" + fname.Replace(".wwm", ".X");
		WWMToDOTXConverter.ConvertWWMToDOTX(inputWWMPath, outputPath);
	}
}
