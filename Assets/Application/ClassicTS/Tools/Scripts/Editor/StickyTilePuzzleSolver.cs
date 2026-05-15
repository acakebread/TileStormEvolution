#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class StickyTilePuzzleSolver : EditorWindow
{
	[MenuItem("Tools/Classic Tilestorm/Puzzles/Sticky Tile Puzzle Solver")]
	public static void ShowWindow()
	{
		GetWindow<StickyTilePuzzleSolver>("Sticky Tile Solver");
	}

	private string[] boardText = new string[]
	{
		"S##S",
		"1342",
		"4213"
	};

	private string[] goalText = new string[]
	{
		"S##S",
		"1212",
		"3434"
	};

	private void OnGUI()
	{
		if (GUILayout.Button("Solve Puzzle"))
		{
			SolvePuzzle(boardText, goalText);
		}
	}

	private struct BoardState
	{
		public string[] board;
		public List<string> moves;

		public BoardState(string[] b, List<string> m)
		{
			board = b;
			moves = new List<string>(m);
		}
	}

	private void SolvePuzzle(string[] start, string[] goal)
	{
		Queue<BoardState> queue = new Queue<BoardState>();
		HashSet<string> visited = new HashSet<string>();

		queue.Enqueue(new BoardState(start, new List<string>()));
		visited.Add(BoardToString(start));

		while (queue.Count > 0)
		{
			BoardState state = queue.Dequeue();

			if (BoardsEqual(state.board, goal))
			{
				Debug.Log("Solution found! Moves:");
				foreach (var move in state.moves)
					Debug.Log(move);
				return;
			}

			// Generate all legal moves
			List<(string moveName, string[] newBoard)> nextStates = GenerateLegalMoves(state.board);

			foreach (var next in nextStates)
			{
				string key = BoardToString(next.newBoard);
				if (!visited.Contains(key))
				{
					var newMoves = new List<string>(state.moves);
					newMoves.Add(next.moveName);
					queue.Enqueue(new BoardState(next.newBoard, newMoves));
					visited.Add(key);
				}
			}
		}

		Debug.LogWarning("No solution found!");
	}

	private List<(string moveName, string[] newBoard)> GenerateLegalMoves(string[] board)
	{
		var results = new List<(string, string[])>();
		int rows = board.Length;
		int cols = board[0].Length;

		// --- ROW MOVES ---
		for (int r = 0; r < rows; r++)
		{
			string row = board[r];

			// Split into contiguous segments separated by '#'
			int start = 0;
			while (start < cols)
			{
				if (row[start] == '#') { start++; continue; }

				int end = start;
				while (end + 1 < cols && row[end + 1] != '#') end++;

				string segment = row.Substring(start, end - start + 1);

				// Left rotation if S at left
				if (segment[0] == 'S' && segment.Length > 1)
				{
					string newSegment = segment.Substring(1) + segment[0];
					string newRow = row.Substring(0, start) + newSegment + row.Substring(end + 1);
					string[] newBoard = CopyBoard(board);
					newBoard[r] = newRow;
					results.Add(($"row{r}_L_{start}-{end}", newBoard));
				}

				// Right rotation if S at right
				if (segment[segment.Length - 1] == 'S' && segment.Length > 1)
				{
					string newSegment = segment[segment.Length - 1] + segment.Substring(0, segment.Length - 1);
					string newRow = row.Substring(0, start) + newSegment + row.Substring(end + 1);
					string[] newBoard = CopyBoard(board);
					newBoard[r] = newRow;
					results.Add(($"row{r}_R_{start}-{end}", newBoard));
				}

				start = end + 1;
			}
		}

		// --- COLUMN MOVES ---
		for (int c = 0; c < cols; c++)
		{
			// Extract column
			char[] col = new char[rows];
			for (int r = 0; r < rows; r++) col[r] = board[r][c];

			int start = 0;
			while (start < rows)
			{
				if (col[start] == '#') { start++; continue; }

				int end = start;
				while (end + 1 < rows && col[end + 1] != '#') end++;

				char[] segment = new char[end - start + 1];
				for (int i = 0; i < segment.Length; i++) segment[i] = col[start + i];

				// Up rotation if S at top
				if (segment[0] == 'S' && segment.Length > 1)
				{
					char[] newSegment = new char[segment.Length];
					for (int i = 0; i < segment.Length - 1; i++) newSegment[i] = segment[i + 1];
					newSegment[segment.Length - 1] = segment[0];

					string[] newBoard = CopyBoard(board);
					for (int i = 0; i < segment.Length; i++)
					{
						char[] rowChars = newBoard[start + i].ToCharArray();
						rowChars[c] = newSegment[i];
						newBoard[start + i] = new string(rowChars);
					}

					results.Add(($"col{c}_U_{start}-{end}", newBoard));
				}

				// Down rotation if S at bottom
				if (segment[segment.Length - 1] == 'S' && segment.Length > 1)
				{
					char[] newSegment = new char[segment.Length];
					newSegment[0] = segment[segment.Length - 1];
					for (int i = 1; i < segment.Length; i++) newSegment[i] = segment[i - 1];

					string[] newBoard = CopyBoard(board);
					for (int i = 0; i < segment.Length; i++)
					{
						char[] rowChars = newBoard[start + i].ToCharArray();
						rowChars[c] = newSegment[i];
						newBoard[start + i] = new string(rowChars);
					}

					results.Add(($"col{c}_D_{start}-{end}", newBoard));
				}

				start = end + 1;
			}
		}

		return results;
	}

	private string[] CopyBoard(string[] board)
	{
		string[] copy = new string[board.Length];
		for (int i = 0; i < board.Length; i++)
			copy[i] = string.Copy(board[i]);
		return copy;
	}

	private string BoardToString(string[] board)
	{
		return string.Join(",", board);
	}

	private bool BoardsEqual(string[] a, string[] b)
	{
		if (a.Length != b.Length) return false;
		for (int i = 0; i < a.Length; i++)
			if (a[i] != b[i]) return false;
		return true;
	}
}
#endif
