using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public GameObject[] Tiles;

	int numx = 32;
	int numy = 32;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		for (var y = 0; y < numy; ++y)
		{
			for (var x = 0; x < numx; ++x)
			{
				var instance = Instantiate(Tiles[Random.Range(0,Tiles.Length)]);
				instance.transform.position = Vector3.forward * (y - numy / 2) * 2 + Vector3.right * (x - numx / 2) * 2;
			}
		}



		//for (int n = 0; n < Tiles.Length; n++)
		//{
		//	var instance = Instantiate(Tiles[n]);
		//	instance.transform.position = Vector3.forward * n * 2;
		//}
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
