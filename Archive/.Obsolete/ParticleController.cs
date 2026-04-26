//using System.Collections.Generic;
//using UnityEngine;

//public class ParticleController : MonoBehaviour
//{
//	[System.Serializable]
//	public class SparkSettings
//	{
//		public Vector3 position = Vector3.zero; // Spawn position
//		public Vector3 velocity = Vector3.up; // Initial velocity
//		public float speed = 12f; // Matches TracerSystem default
//		public float lifetime = 1f; // Matches TracerSystem default
//		public float width = 1.25f; // Matches TracerSystem default
//		public Color color = Color.white; // Matches TracerSystem default
//		public float gravity = 0.25f; // Damping force (mVel.y -= 0.25f * deltaTime * SIM_SPEED)
//		public float moveScale = 50f; // Velocity scale (mPos += mVel * 50.0f * deltaTime * SIM_SPEED)
//		public float bounceDamping = 0.5f; // Velocity damping on collision (mVel.y *= -0.5f)
//		public float groundHeight = 0f; // Ground plane Y position
//	}

//	[SerializeField] private TracerSystem tracerSystem; // Reference to TracerSystem
//	[SerializeField] private SparkSettings sparkSettings;

//	private class Spark
//	{
//		public Vector3 position;
//		public Vector3 velocity;
//		public float lifetime;
//		public float maxLifetime;
//		public float width;
//		public Color color;
//		public bool isActive;
//	}

//	private readonly float simSpeed = 1f; // SIM_SPEED from DirectX sample
//	private List<Spark> sparkPool = new List<Spark>();
//	[SerializeField] private int maxSparks = 256;

//	void Awake()
//	{
//		// Initialize spark pool
//		for (int i = 0; i < maxSparks; i++)
//		{
//			sparkPool.Add(new Spark { isActive = false });
//		}

//		// Validate TracerSystem reference
//		if (tracerSystem == null)
//		{
//			enabled = false;
//			throw new System.Exception("ParticleController: TracerSystem reference is not assigned.");
//		}
//	}

//	void Update()
//	{
//		UpdateSparks();
//	}

//	public void SpawnSpark(Vector3 position, Vector3 velocity, SparkSettings settings = null)
//	{
//		if (settings == null) settings = sparkSettings;

//		Spark spark = GetInactiveSpark();
//		if (spark == null) return;

//		spark.position = position;
//		spark.velocity = velocity.normalized * settings.speed;
//		spark.lifetime = settings.lifetime;
//		spark.maxLifetime = settings.lifetime;
//		spark.width = settings.width;
//		spark.color = settings.color;
//		spark.isActive = true;

//		// Spawn in TracerSystem
//		TracerSystem.TracerSettings tracerSettings = new TracerSystem.TracerSettings
//		{
//			speed = settings.speed,
//			length = tracerSystem.defaultSettings.length, // Use TracerSystem's length
//			lifetime = settings.lifetime,
//			width = settings.width,
//			color = settings.color
//		};
//		tracerSystem.SpawnTracer(position, velocity.normalized, tracerSettings);
//	}

//	private Spark GetInactiveSpark()
//	{
//		foreach (Spark spark in sparkPool)
//		{
//			if (!spark.isActive) return spark;
//		}
//		return null; // Pool exhausted
//	}

//	private void UpdateSparks()
//	{
//		float deltaTime = Time.deltaTime;

//		foreach (Spark spark in sparkPool)
//		{
//			if (!spark.isActive) continue;

//			// Update lifetime (handled by TracerSystem, but tracked here for consistency)
//			spark.lifetime -= deltaTime;
//			if (spark.lifetime <= 0f)
//			{
//				spark.isActive = false;
//				continue;
//			}

//			// Apply gravity damping (mVel.y -= 0.25f * nElapsed * 0.001f * SIM_SPEED)
//			spark.velocity.y -= sparkSettings.gravity * deltaTime * simSpeed;

//			// Update position (mPos += mVel * 50.0f * nElapsed * 0.001f * SIM_SPEED)
//			spark.position += spark.velocity * sparkSettings.moveScale * deltaTime * simSpeed;

//			// Ground collision (collide = GetSize() * 0.75f, using width)
//			float collide = spark.width * 0.75f;
//			if (spark.velocity.y < 0 && spark.position.y < sparkSettings.groundHeight + collide)
//			{
//				// Reflect position (mPos.y = collide * 2.0f - mPos.y)
//				spark.position.y = sparkSettings.groundHeight + (collide * 2f - (spark.position.y - sparkSettings.groundHeight));
//				// Bounce with damping (mVel.y = -mVel.y * 0.5f)
//				spark.velocity.y = -spark.velocity.y * sparkSettings.bounceDamping;
//			}
//		}
//	}
//}