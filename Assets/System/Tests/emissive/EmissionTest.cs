using UnityEngine;

public class EmissionTest : MonoBehaviour
{
	private MeshRenderer mr;
	private Material mat;

	void Start()
	{
		mr = GetComponent<MeshRenderer>();
		mat = mr.material; // Use the preallocated material
		//if (mat != null)
		//{
		//	mat.EnableKeyword("_EMISSION"); // Ensure emission is enabled
		//	mat.SetColor("_EmissionColor", new Color(0f, 1f, 0f) * 2.5f); // Bright green
		//	Debug.Log("Emission Test Started - Emission Enabled: " + mat.IsKeywordEnabled("_EMISSION") + ", Emission Color: " + mat.GetColor("_EmissionColor") + ", Material Instance: " + mat.GetInstanceID());
		//}
		//else
		//{
		//	Debug.LogError("Material not found on MeshRenderer!");
		//}
	}

	void Update()
	{
		//if (mr != null && mr.material != null)
		//{
		//	Debug.Log("Emission Update - Enabled: " + mr.material.IsKeywordEnabled("_EMISSION") + ", Color: " + mr.material.GetColor("_EmissionColor") + ", Instance: " + mr.material.GetInstanceID());
		//}

		if (mat != null)
		{
			//mat.EnableKeyword("_EMISSION"); // Ensure emission is enabled
			mat.SetColor("_EmissionColor", new Color(0f, 1f, 0f) * (Mathf.Sin(Time.time * 6f) * 0.5f + 1.5f)); // Bright green
			//Debug.Log("Emission Test Started - Emission Enabled: " + mat.IsKeywordEnabled("_EMISSION") + ", Emission Color: " + mat.GetColor("_EmissionColor") + ", Material Instance: " + mat.GetInstanceID());
		}
		else
		{
			Debug.LogError("Material not found on MeshRenderer!");
		}
	}
}