using UnityEngine;

public class FPSDebug : MonoBehaviour
{
	// ---------- Config ----------
	[Header("Appearance")]
	public int fontSize = 18;
	public Color textColor = Color.white;
	public Color bgColor = new Color(0, 0, 0, 0.7f);
	public int padding = 10;
	public int chartHeight = 40;
	public int chartSamples = 120;     // total raw frames (including pre-fill)
	public int samplesPerBar = 10;      // 10 frames → 1 bar
	public int barGap = 1;       // 1px gap between bars

	[Header("Peak Tracking")]
	public int peakHistorySamples = 600;   // ~10 s at 60 FPS

	[Header("Smoothing")]
	[Range(0.01f, 1f)] public float smoothFactor = 0.1f;

	[Header("Text Update Rate")]
	public float textUpdateInterval = 0.25f; // 4 Hz

	// ---------- Runtime ----------
	private float deltaTime = 0f;
	private GUIStyle style;
	private Texture2D bgTex;
	private Material glMat;

	private float[] rawHistory;
	private int head = 0;

	private float[] peakHistory;
	private int peakHead = 0;
	private float currentMaxFPS = 120f;

	private float nextTextUpdate = 0f;
	private string cachedText = "";

	void Awake()
	{
		bgTex = MakeTex(1, 1, bgColor);

		style = new GUIStyle();
		style.fontSize = fontSize;
		style.normal.textColor = textColor;
		style.normal.background = bgTex;
		style.alignment = TextAnchor.UpperRight;

		// Pre-fill with zeros
		rawHistory = new float[chartSamples];
		for (int i = 0; i < chartSamples; i++)
			rawHistory[i] = 0f;

		peakHistory = new float[peakHistorySamples];

		Shader s = Shader.Find("Hidden/Internal-Colored");
		glMat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
		glMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		glMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		glMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
		glMat.SetInt("_ZWrite", 0);
	}

	void Update()
	{
		deltaTime += (Time.unscaledDeltaTime - deltaTime) * smoothFactor;
		float fps = 1f / Mathf.Max(deltaTime, 0.0001f);

		// Overwrite oldest entry
		rawHistory[head] = fps;
		head = (head + 1) % chartSamples;

		peakHistory[peakHead] = fps;
		peakHead = (peakHead + 1) % peakHistorySamples;

		// 4 Hz update
		if (Time.unscaledTime >= nextTextUpdate)
		{
			float msec = deltaTime * 1000f;
			cachedText = $"{fps:0.} FPS ({msec:0.0} ms)";

			// Update peak
			float peak = 0f;
			for (int i = 0; i < peakHistorySamples; i++)
				if (peakHistory[i] > peak) peak = peakHistory[i];

			currentMaxFPS = Mathf.Lerp(currentMaxFPS, peak, 0.1f);
			currentMaxFPS = Mathf.Clamp(currentMaxFPS, 30f, 300f);

			nextTextUpdate = Time.unscaledTime + textUpdateInterval;
		}
	}

	void OnGUI()
	{
		Vector2 size = style.CalcSize(new GUIContent(cachedText));
		int w = Screen.width, h = Screen.height;

		Rect labelRect = new Rect(w - size.x - padding, padding, size.x, size.y);
		Rect bgRect = new Rect(
			labelRect.x - padding,
			labelRect.y - padding,
			labelRect.width + 2 * padding,
			labelRect.height + 2 * padding + chartHeight + padding);

		GUI.DrawTexture(bgRect, bgTex);
		GUI.Label(labelRect, cachedText, style);

		DrawChart(labelRect, bgRect);
	}

	private void DrawChart(Rect labelRect, Rect fullBgRect)
	{
		Rect chartRect = new Rect(
			fullBgRect.x + padding,
			labelRect.yMax + padding,
			fullBgRect.width - 2 * padding,
			chartHeight);

		Color chartBg = new Color(bgColor.r, bgColor.g, bgColor.b, bgColor.a * 0.6f);
		GUI.DrawTexture(chartRect, MakeTex(1, 1, chartBg));

		if (Event.current.type != EventType.Repaint) return;

		int barCount = Mathf.CeilToInt((float)chartSamples / samplesPerBar);
		barCount = Mathf.Min(barCount, 50);
		if (barCount == 0) return;

		// Layout with gaps
		int totalGaps = Mathf.Max(0, barCount - 1) * barGap;
		float availableWidth = chartRect.width - totalGaps;
		float barWidth = availableWidth / barCount;

		glMat.SetPass(0);
		GL.PushMatrix();
		GL.LoadPixelMatrix();
		GL.Begin(GL.QUADS);
		GL.Color(new Color(0, 0.8f, 1f, 0.75f));

		float x = chartRect.x;

		// Start from oldest sample (left = oldest)
		int oldestIdx = head; // because we overwrite in order, head points to next write → oldest is current head

		for (int bar = 0; bar < barCount; bar++)
		{
			float sum = 0f;
			int cnt = 0;

			for (int i = 0; i < samplesPerBar; i++)
			{
				int idx = (oldestIdx + bar * samplesPerBar + i) % chartSamples;
				sum += rawHistory[idx];
				cnt++;
			}

			float avg = sum / cnt;
			float x0 = x;
			float x1 = x + barWidth;
			float norm = Mathf.Clamp01(avg / currentMaxFPS);
			float yTop = chartRect.yMax - norm * chartRect.height;
			float yBot = chartRect.yMax;

			GL.Vertex3(x0, yBot, 0);
			GL.Vertex3(x1, yBot, 0);
			GL.Vertex3(x1, yTop, 0);
			GL.Vertex3(x0, yTop, 0);

			x = x1 + barGap;
		}

		GL.End();
		GL.PopMatrix();

		// Dynamic reference lines
		float step = currentMaxFPS * 0.25f;
		DrawGLRefLine(chartRect, step * 1 / currentMaxFPS, new Color(1, 1, 1, 0.2f));
		DrawGLRefLine(chartRect, step * 2 / currentMaxFPS, new Color(1, 1, 1, 0.2f));
		DrawGLRefLine(chartRect, step * 3 / currentMaxFPS, new Color(1, 1, 1, 0.2f));
	}

	private void DrawGLRefLine(Rect r, float normY, Color col)
	{
		float y = r.yMax - normY * r.height;
		GL.PushMatrix();
		GL.LoadPixelMatrix();
		GL.Begin(GL.LINES);
		GL.Color(col);
		GL.Vertex3(r.x, y, 0);
		GL.Vertex3(r.xMax, y, 0);
		GL.End();
		GL.PopMatrix();
	}

	private Texture2D MakeTex(int w, int h, Color col)
	{
		Texture2D tex = new Texture2D(w, h);
		Color[] pix = new Color[w * h];
		for (int i = 0; i < pix.Length; i++) pix[i] = col;
		tex.SetPixels(pix);
		tex.Apply();
		return tex;
	}

	void OnDestroy()
	{
		if (glMat) DestroyImmediate(glMat);
	}
}