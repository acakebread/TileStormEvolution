//#nullable enable
//using UnityEngine;
//using UnityEditor;
//using System.Collections.Generic;

//public class SlidePuzzleSolver : EditorWindow
//{
//	private string startBoard = "EABCIFGDMJKHNOL.";   // Your puzzle
//	private string solution = "";
//	private Vector2 scrollPos;
//	private bool isSolving = false;

//	// CORRECT goal for your specific puzzle (blank in bottom-right)
//	private static readonly string GOAL = "ABCDEFGHIJKLMNO.";

//	[MenuItem("Tools/Classic Tilestorm/Slide Puzzle Solver")]
//	public static void ShowWindow()
//	{
//		var win = GetWindow<SlidePuzzleSolver>("Slide Puzzle Solver");
//		win.minSize = new Vector2(420, 640);
//	}

//	void OnGUI()
//	{
//		GUILayout.Label("4×4 Slide Puzzle Solver", EditorStyles.boldLabel);
//		GUILayout.Space(10);

//		EditorGUILayout.HelpBox("Enter 16 characters, use '.' for empty tile.\nYour puzzle → EABCIFGDMJKHNOL.", MessageType.Info);

//		startBoard = EditorGUILayout.TextField("Puzzle", startBoard)
//								 .Replace(" ", ".").ToUpperInvariant();

//		if (GUILayout.Button("Solve Puzzle", GUILayout.Height(40)))
//			SolvePuzzle();

//		if (isSolving)
//		{
//			EditorGUILayout.HelpBox("Searching optimal solution...", MessageType.Warning);
//			Repaint();
//			return;
//		}

//		if (!string.IsNullOrEmpty(solution))
//		{
//			if (solution == "UNSOLVABLE")
//				EditorGUILayout.HelpBox("This configuration is mathematically impossible.", MessageType.Error);
//			else if (solution == "INVALID")
//				EditorGUILayout.HelpBox("Invalid input – exactly 16 chars and one '.' required.", MessageType.Error);
//			else
//			{
//				EditorGUILayout.HelpBox($"SOLVED in {solution.Length} moves (optimal)", MessageType.Info);
//				GUILayout.Label("Moves (U=Up, D=Down, L=Left, R=Right):", EditorStyles.boldLabel);
//				GUILayout.Label(solution, new GUIStyle(EditorStyles.textField) { fontSize = 16, fontStyle = FontStyle.Bold });

//				scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(400));
//				ShowSteps();
//				EditorGUILayout.EndScrollView();
//			}
//		}
//	}

//	void SolvePuzzle()
//	{
//		isSolving = true;
//		solution = "";

//		string board = startBoard.Replace(" ", ".").ToUpperInvariant();
//		if (board.Length != 16 || board.IndexOf('.') < 0 || board.Replace(".", "").Length != 15)
//		{
//			solution = "INVALID";
//			isSolving = false;
//			return;
//		}

//		// Correct solvability for blank in bottom-right
//		if (!IsSolvable(board))
//		{
//			solution = "UNSOLVABLE";
//			isSolving = false;
//			return;
//		}

//		solution = PuzzleSolver.Solve(board) ?? "ERROR";
//		isSolving = false;
//	}

//	void ShowSteps()
//	{
//		string current = startBoard.Replace(" ", ".").ToUpperInvariant();
//		int empty = current.IndexOf('.');

//		DrawBoard("Start", current);

//		int step = 1;
//		foreach (char move in solution)
//		{
//			int r = empty / 4, c = empty % 4;
//			int nr = r, nc = c;
//			switch (move)
//			{
//				case 'U': nr--; break;
//				case 'D': nr++; break;
//				case 'L': nc--; break;
//				case 'R': nc++; break;
//			}
//			int target = nr * 4 + nc;

//			var arr = current.ToCharArray();
//			arr[empty] = arr[target];
//			arr[target] = '.';
//			current = new string(arr);
//			empty = target;

//			EditorGUILayout.LabelField($"Step {step++}: → {move}", EditorStyles.boldLabel);
//			DrawBoard("", current);
//		}

//		DrawBoard("GOAL!", GOAL);
//	}

//	void DrawBoard(string caption, string b)
//	{
//		if (!string.IsNullOrEmpty(caption))
//			EditorGUILayout.LabelField(caption, EditorStyles.boldLabel);

//		GUILayout.BeginHorizontal();
//		for (int i = 0; i < 16; i++)
//		{
//			string t = b[i] == '.' ? " " : b[i].ToString();
//			GUI.backgroundColor = t == " " ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.1f, 0.7f, 1f);
//			GUILayout.Box(t, GUILayout.Width(64), GUILayout.Height(64));
//			if ((i + 1) % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
//		}
//		GUILayout.EndHorizontal();
//		GUI.backgroundColor = Color.white;
//		GUILayout.Space(10);
//	}

//	// Fixed solvability test for blank in bottom-right corner
//	bool IsSolvable(string board)
//	{
//		int inversions = 0;
//		for (int i = 0; i < 16; i++)
//		{
//			if (board[i] == '.') continue;
//			for (int j = i + 1; j < 16; j++)
//			{
//				if (board[j] != '.' && GOAL.IndexOf(board[i]) > GOAL.IndexOf(board[j]))
//					inversions++;
//			}
//		}
//		return inversions % 2 == 0;   // Even = solvable when blank is bottom-right
//	}
//}

//// =============================================
//// Pure, fast BFS solver (no Unity dependencies)
//// =============================================
//public static class PuzzleSolver
//{
//	class Node
//	{
//		public string Board;
//		public int EmptyPos;
//		public string Path;
//		public Node(string b, int e, string p = "") { Board = b; EmptyPos = e; Path = p; }
//		public override int GetHashCode() => Board.GetHashCode();
//		public override bool Equals(object? obj) => obj is Node n && n.Board == Board;
//	}

//	private static readonly string Goal = "ABCDEFGHIJKLMNO.";
//	private static readonly (int dr, int dc, char dir)[] Dirs = {
//		(-1, 0, 'U'), (1, 0, 'D'), (0, -1, 'L'), (0, 1, 'R')
//	};

//	public static string Solve(string start)
//	{
//		int empty = start.IndexOf('.');
//		var queue = new Queue<Node>();
//		var visited = new HashSet<Node>();

//		queue.Enqueue(new Node(start, empty));
//		visited.Add(queue.Peek());

//		while (queue.Count > 0)
//		{
//			var cur = queue.Dequeue();
//			if (cur.Board == Goal) return cur.Path;

//			int r = cur.EmptyPos / 4;
//			int c = cur.EmptyPos % 4;

//			foreach (var (dr, dc, dir) in Dirs)
//			{
//				int nr = r + dr, nc = c + dc;
//				if (nr >= 0 && nr < 4 && nc >= 0 && nc < 4)
//				{
//					int np = nr * 4 + nc;
//					char[] next = cur.Board.ToCharArray();
//					next[cur.EmptyPos] = next[np];
//					next[np] = '.';

//					var child = new Node(new string(next), np, cur.Path + dir);
//					if (visited.Add(child))
//						queue.Enqueue(child);
//				}
//			}
//		}
//		return null;
//	}
//}