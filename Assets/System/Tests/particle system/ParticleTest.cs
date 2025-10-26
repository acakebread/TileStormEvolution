using MassiveHadronLtd;
using System.Collections;
using UnityEngine;

public class ParticleTest : MonoBehaviour
{
	//[SerializeField] private ParticleController particleController;

	//void Update()
	//{
	//	//if (Input.GetKey(KeyCode.Space))
	//	{
	//		//GetComponent<TracerSystem>().SpawnTestTracer();
	//		particleController.SpawnSpark(Vector3.zero, Random.onUnitSphere);
	//	}
	//}

	//[SerializeField] private new ParticleSystem particleSystem;
	[SerializeField] private SparkController sparkController;
	[SerializeField][Range(0, 10)] private float delay = 0.1f;
	[SerializeField][Range(1, 32)] private int ct = 1;
	[SerializeField] bool continuous = true;

	[SerializeField] private Vector3 spawnOffset = Vector3.zero;

	IEnumerator Start()
	{
		//SparkSystem.SparkSettings settings = new SparkSystem.SparkSettings
		//{
		//	speed = sparkSystem.defaultSettings.speed,
		//	length = sparkSystem.defaultSettings.length,
		//	tipSize = sparkSystem.defaultSettings.tipSize,
		//	lifetime = Random.Range(lifetimeRange.x, lifetimeRange.y),
		//	width = sparkSystem.defaultSettings.width,
		//	decay = sparkSystem.defaultSettings.decay,
		//	useAdditiveBlending = sparkSystem.defaultSettings.useAdditiveBlending,
		//	color = sparkSystem.defaultSettings.color,
		//	gravity = sparkSystem.defaultSettings.gravity,
		//	moveScale = sparkSystem.defaultSettings.moveScale,
		//	bounceDamping = sparkSystem.defaultSettings.bounceDamping,
		//	groundHeight = sparkSystem.defaultSettings.groundHeight
		//};

		while (true)
		{
			if (continuous || Input.GetKey(KeyCode.Space))
			{
				for (var n = 0; n < ct; ++n)
				{
					var vel = Random.onUnitSphere * Random.value * 0.5f;
					vel.y *= 2f; // exaggerate vertical speed
					vel += Vector3.up;
					Vector3 worldPos = sparkController.transform.position + spawnOffset;
					//sparkSystem.SpawnSpark(worldPos, vel, settings);
					sparkController.SpawnSpark(worldPos, vel);//
				}
			}
			yield return new WaitForSeconds(delay);
		}
	}
}


//using MassiveHadronLtd;
//using System.Collections;
//using UnityEngine;

//public class ParticleTest : MonoBehaviour
//{
//	//[SerializeField] private ParticleController particleController;

//	//void Update()
//	//{
//	//	//if (Input.GetKey(KeyCode.Space))
//	//	{
//	//		//GetComponent<TracerSystem>().SpawnTestTracer();
//	//		particleController.SpawnSpark(Vector3.zero, Random.onUnitSphere);
//	//	}
//	//}

//	[SerializeField] private SparkSystem sparkSystem;
//	[SerializeField][Range(0, 10)] private float delay = 0.1f;
//	[SerializeField][Range(1, 32)] private int ct = 1;
//	[SerializeField] bool continuous = true;

//	[SerializeField] private Vector2 lifetimeRange = new Vector2(1f, 5f);
//	[SerializeField] private Vector3 spawnOffset = Vector3.zero;

//	IEnumerator Start()
//	{
//		//SparkSystem.SparkSettings settings = new SparkSystem.SparkSettings
//		//{
//		//	speed = sparkSystem.defaultSettings.speed,
//		//	length = sparkSystem.defaultSettings.length,
//		//	tipSize = sparkSystem.defaultSettings.tipSize,
//		//	lifetime = Random.Range(lifetimeRange.x, lifetimeRange.y),
//		//	width = sparkSystem.defaultSettings.width,
//		//	decay = sparkSystem.defaultSettings.decay,
//		//	useAdditiveBlending = sparkSystem.defaultSettings.useAdditiveBlending,
//		//	color = sparkSystem.defaultSettings.color,
//		//	gravity = sparkSystem.defaultSettings.gravity,
//		//	moveScale = sparkSystem.defaultSettings.moveScale,
//		//	bounceDamping = sparkSystem.defaultSettings.bounceDamping,
//		//	groundHeight = sparkSystem.defaultSettings.groundHeight
//		//};

//		while (true)
//		{
//			if (continuous || Input.GetKey(KeyCode.Space))
//			{
//				for (var n = 0; n < ct; ++n)
//				{
//					var vel = Random.onUnitSphere * Random.value * 0.5f;
//					vel.y *= 2f; // exaggerate vertical speed
//					vel += Vector3.up;
//					Vector3 worldPos = sparkSystem.transform.position + spawnOffset;
//					//sparkSystem.SpawnSpark(worldPos, vel, settings);
//					sparkSystem.SpawnSpark(worldPos, vel);//
//				}
//			}
//			yield return new WaitForSeconds(delay);
//		}
//	}
//}
