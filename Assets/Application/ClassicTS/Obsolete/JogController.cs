//using UnityEngine;

//public class JogController : MonoBehaviour
//{
//	private const int NUM_JOG = 8; // Matches NUM_JOG from JogCtrl.h

//	public class JogCtrl
//	{
//		public enum Mode
//		{
//			Null,
//			Control,
//			Process
//		}

//		private Mode mode = Mode.Null;
//		private Tile[] tiles; // Array of tiles to jog
//		private int count; // Number of tiles
//		private int lockMin, lockMax; // Movement bounds
//		private float vec = 0f; // Position offset
//		private float vel = 0f; // Velocity
//		private bool isHorizontal; // Direction of jogging (true = X, false = Z)

//		public void Init(Tile[] tiles, int count, int lockMin, int lockMax, bool isHorizontal)
//		{
//			this.tiles = tiles;
//			this.count = count;
//			this.lockMin = lockMin;
//			this.lockMax = lockMax;
//			this.isHorizontal = isHorizontal;
//			mode = Mode.Control;
//			vec = 0f;
//			vel = 0f;
//		}

//		public float GetVec() => vec;
//		public Mode GetMode() => mode;
//		public void SetMode(Mode newMode) => mode = newMode;

//		private float Clamp(float vec)
//		{
//			if (lockMax >= lockMin)
//			{
//				float min = -lockMin;
//				float max = count - lockMax - 1;
//				return Mathf.Clamp(vec, min, max);
//			}
//			return vec;
//		}

//		private void Adjust(int adjust)
//		{
//			if (adjust != 0 && tiles != null)
//			{
//				lockMin += adjust;
//				lockMax += adjust;
//				foreach (Tile tile in tiles)
//				{
//					if (isHorizontal)
//						tile.transform.position += new Vector3(adjust, 0, 0);
//					else
//						tile.transform.position += new Vector3(0, 0, adjust);
//				}
//			}
//		}

//		public bool Update()
//		{
//			const float speed = 0.15f;
//			float phase = (vec % 1f - 0.5f) * 2f;

//			if (phase > 0f) vec += speed;
//			else if (phase < 0f) vec -= speed;

//			float newPhase = (vec % 1f - 0.5f) * 2f;
//			if ((phase <= 0f && newPhase >= 0f) || (phase >= 0f && newPhase <= 0f))
//			{
//				vec = Mathf.Floor(vec + 0.5f);
//				vel = 0f;
//			}

//			float clampedVec = Clamp(vec);
//			Adjust((int)Mathf.Floor(clampedVec));

//			if (vec != clampedVec)
//			{
//				vec = 0f;
//				vel = 0f;
//			}
//			else
//			{
//				vec -= Mathf.Floor(vec);
//			}

//			return vec != 0f || vel != 0f;
//		}

//		public void ApplyVec(ref float dragVec)
//		{
//			vel = dragVec;
//			float min = vec > 0f ? 0f : -1f;
//			vec += dragVec;

//			float clampedVec = Clamp(vec);
//			clampedVec = Mathf.Clamp(clampedVec, min, 1f);

//			bool clamped = (dragVec < 0f && vec < clampedVec) || (dragVec > 0f && vec > clampedVec);
//			int adjust = (int)Mathf.Floor(clampedVec);
//			Adjust(adjust);

//			float newVec = vec - clampedVec;
//			vec -= Mathf.Floor(vec);

//			dragVec = clamped ? newVec : 0f;
//			if (clamped) vec = 0f;
//		}
//	}

//	private JogCtrl[] jogCtrls = new JogCtrl[NUM_JOG];
//	private int activeJog = -1;
//	private MapLoader mapLoader;

//	void Start()
//	{
//		mapLoader = GetComponent<MapLoader>();
//		if (mapLoader == null)
//		{
//			Debug.LogError("MapLoader component not found on this GameObject!");
//			return;
//		}

//		for (int i = 0; i < NUM_JOG; i++)
//		{
//			jogCtrls[i] = new JogCtrl();
//		}
//	}

//	void Update()
//	{
//		for (int i = 0; i < NUM_JOG; i++)
//		{
//			if (jogCtrls[i].GetMode() == JogCtrl.Mode.Process)
//			{
//				if (!jogCtrls[i].Update())
//				{
//					jogCtrls[i].SetMode(JogCtrl.Mode.Null);
//				}
//			}
//		}
//	}

//	public int CreateJogControl(Tile[] tiles, int count, int lockMin, int lockMax, bool isHorizontal)
//	{
//		for (int i = 0; i < NUM_JOG; i++)
//		{
//			if (jogCtrls[i].GetMode() == JogCtrl.Mode.Null)
//			{
//				jogCtrls[i].Init(tiles, count, lockMin, lockMax, isHorizontal);
//				activeJog = i;
//				return i;
//			}
//		}
//		return -1;
//	}

//	public void BeginDrag(string mapName, int x, int z)
//	{
//		Tile[,] grid = mapLoader.GetMapGrid(mapName); // Use the generic getter
//		if (grid == null)
//		{
//			Debug.LogError($"Map '{mapName}' not found in TileGrids!");
//			return;
//		}

//		if (x < 0 || x >= grid.GetLength(0) || z < 0 || z >= grid.GetLength(1))
//		{
//			Debug.LogWarning($"Coordinates ({x}, {z}) out of bounds for map '{mapName}'!");
//			return;
//		}

//		Tile tile = grid[x, z];
//		if (tile != null && (tile.GetSlide() || tile.GetRoll()))
//		{
//			// Example: Create a jog control for a horizontal row
//			Tile[] row = new Tile[grid.GetLength(0)];
//			for (int i = 0; i < row.Length; i++)
//				row[i] = grid[i, z];
//			activeJog = CreateJogControl(row, row.Length, 0, row.Length - 1, true);
//		}
//	}

//	public void ApplyDrag(float dragAmount)
//	{
//		if (activeJog >= 0 && activeJog < NUM_JOG)
//		{
//			jogCtrls[activeJog].ApplyVec(ref dragAmount);
//		}
//	}

//	public void EndDrag()
//	{
//		if (activeJog >= 0 && activeJog < NUM_JOG)
//		{
//			jogCtrls[activeJog].SetMode(JogCtrl.Mode.Process);
//			activeJog = -1;
//		}
//	}
//}


//using UnityEngine;

//public class JogController : MonoBehaviour
//{
//	private const int NUM_JOG = 8; // Matches NUM_JOG from JogCtrl.h

//	public class JogCtrl
//	{
//		public enum Mode
//		{
//			Null,
//			Control,
//			Process
//		}

//		private Mode mode = Mode.Null;
//		private Tile[] tiles; // Array of tiles to jog
//		private int count; // Number of tiles
//		private int lockMin, lockMax; // Movement bounds
//		private float vec = 0f; // Position offset
//		private float vel = 0f; // Velocity
//		private bool isHorizontal; // Direction of jogging (true = X, false = Z)

//		public void Init(Tile[] tiles, int count, int lockMin, int lockMax, bool isHorizontal)
//		{
//			this.tiles = tiles;
//			this.count = count;
//			this.lockMin = lockMin;
//			this.lockMax = lockMax;
//			this.isHorizontal = isHorizontal;
//			mode = Mode.Control;
//			vec = 0f;
//			vel = 0f;
//		}

//		public float GetVec() => vec;
//		public Mode GetMode() => mode;
//		public void SetMode(Mode newMode) => mode = newMode;

//		private float Clamp(float vec)
//		{
//			if (lockMax >= lockMin)
//			{
//				float min = -lockMin;
//				float max = count - lockMax - 1;
//				return Mathf.Clamp(vec, min, max);
//			}
//			return vec;
//		}

//		private void Adjust(int adjust)
//		{
//			if (adjust != 0 && tiles != null)
//			{
//				lockMin += adjust;
//				lockMax += adjust;
//				foreach (Tile tile in tiles)
//				{
//					if (isHorizontal)
//						tile.transform.position += new Vector3(adjust, 0, 0);
//					else
//						tile.transform.position += new Vector3(0, 0, adjust);
//				}
//			}
//		}

//		public bool Update()
//		{
//			const float speed = 0.15f;
//			float phase = (vec % 1f - 0.5f) * 2f;

//			if (phase > 0f) vec += speed;
//			else if (phase < 0f) vec -= speed;

//			float newPhase = (vec % 1f - 0.5f) * 2f;
//			if ((phase <= 0f && newPhase >= 0f) || (phase >= 0f && newPhase <= 0f))
//			{
//				vec = Mathf.Floor(vec + 0.5f);
//				vel = 0f;
//			}

//			float clampedVec = Clamp(vec);
//			Adjust((int)Mathf.Floor(clampedVec));

//			if (vec != clampedVec)
//			{
//				vec = 0f;
//				vel = 0f;
//			}
//			else
//			{
//				vec -= Mathf.Floor(vec);
//			}

//			return vec != 0f || vel != 0f;
//		}

//		public void ApplyVec(ref float dragVec)
//		{
//			vel = dragVec;
//			float min = vec > 0f ? 0f : -1f;
//			vec += dragVec;

//			float clampedVec = Clamp(vec);
//			clampedVec = Mathf.Clamp(clampedVec, min, 1f);

//			bool clamped = (dragVec < 0f && vec < clampedVec) || (dragVec > 0f && vec > clampedVec);
//			int adjust = (int)Mathf.Floor(clampedVec);
//			Adjust(adjust);

//			float newVec = vec - clampedVec;
//			vec -= Mathf.Floor(vec);

//			dragVec = clamped ? newVec : 0f;
//			if (clamped) vec = 0f;
//		}
//	}

//	private JogCtrl[] jogCtrls = new JogCtrl[NUM_JOG];
//	private int activeJog = -1;
//	private MapLoader mapLoader;

//	void Start()
//	{
//		mapLoader = GetComponent<MapLoader>();
//		if (mapLoader == null)
//		{
//			Debug.LogError("MapLoader component not found on this GameObject!");
//			return;
//		}

//		for (int i = 0; i < NUM_JOG; i++)
//		{
//			jogCtrls[i] = new JogCtrl();
//		}
//	}

//	void Update()
//	{
//		for (int i = 0; i < NUM_JOG; i++)
//		{
//			if (jogCtrls[i].GetMode() == JogCtrl.Mode.Process)
//			{
//				if (!jogCtrls[i].Update())
//				{
//					jogCtrls[i].SetMode(JogCtrl.Mode.Null);
//				}
//			}
//		}
//	}

//	public int CreateJogControl(Tile[] tiles, int count, int lockMin, int lockMax, bool isHorizontal)
//	{
//		for (int i = 0; i < NUM_JOG; i++)
//		{
//			if (jogCtrls[i].GetMode() == JogCtrl.Mode.Null)
//			{
//				jogCtrls[i].Init(tiles, count, lockMin, lockMax, isHorizontal);
//				activeJog = i;
//				return i;
//			}
//		}
//		return -1;
//	}

//	public void BeginDrag(string mapName, int x, int z)
//	{
//		Tile[,] grid = mapLoader.GetMapGrid(mapName); // Use the generic getter
//		if (grid == null)
//		{
//			Debug.LogError($"Map '{mapName}' not found in TileGrids!");
//			return;
//		}

//		if (x < 0 || x >= grid.GetLength(0) || z < 0 || z >= grid.GetLength(1))
//		{
//			Debug.LogWarning($"Coordinates ({x}, {z}) out of bounds for map '{mapName}'!");
//			return;
//		}

//		Tile tile = grid[x, z];
//		if (tile != null && (tile.GetSlide() || tile.GetRoll()))
//		{
//			// Example: Create a jog control for a horizontal row
//			Tile[] row = new Tile[grid.GetLength(0)];
//			for (int i = 0; i < row.Length; i++)
//				row[i] = grid[i, z];
//			activeJog = CreateJogControl(row, row.Length, 0, row.Length - 1, true);
//		}
//	}

//	public void ApplyDrag(float dragAmount)
//	{
//		if (activeJog >= 0 && activeJog < NUM_JOG)
//		{
//			jogCtrls[activeJog].ApplyVec(ref dragAmount);
//		}
//	}

//	public void EndDrag()
//	{
//		if (activeJog >= 0 && activeJog < NUM_JOG)
//		{
//			jogCtrls[activeJog].SetMode(JogCtrl.Mode.Process);
//			activeJog = -1;
//		}
//	}
//}