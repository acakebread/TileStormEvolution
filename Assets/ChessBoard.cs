using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

class ChessBoard
{
	private char[,] board;
	private Dictionary<char, int> pieceOrder;

	public ChessBoard(char[,] initialState = null)
	{
		board = new char[8, 8];
		pieceOrder = new Dictionary<char, int>
		{
			{'K', 0}, {'Q', 1}, {'R', 2}, {'B', 3}, {'N', 4}, {'P', 5},
			{'k', 6}, {'q', 7}, {'r', 8}, {'b', 9}, {'n', 10}, {'p', 11}, {'.', 12}
		};

		if (initialState == null)
			InitializeBoard();
		else
			board = initialState;
	}

	private void InitializeBoard()
	{
		string[] defaultBoard = {
			"rnbqkbnr",
			"pppppppp",
			"........",
			"........",
			"........",
			"........",
			"PPPPPPPP",
			"RNBQKBNR"
		};

		for (int i = 0; i < 8; i++)
		{
			for (int j = 0; j < 8; j++)
			{
				board[i, j] = defaultBoard[i][j];
			}
		}
	}

	public int Encode()
	{
		List<int> permutation = new List<int>();
		foreach (char piece in board)
		{
			permutation.Add(pieceOrder[piece]);
		}
		return PermuradicEncode(permutation);
	}

	public void Decode(int index)
	{
		List<int> permutation = PermuradicDecode(index, 64);
		char[,] newBoard = new char[8, 8];
		int count = 0;

		for (int i = 0; i < 8; i++)
		{
			for (int j = 0; j < 8; j++)
			{
				newBoard[i, j] = pieceOrder.FirstOrDefault(x => x.Value == permutation[count]).Key;
				count++;
			}
		}
		board = newBoard;
	}

	private int PermuradicEncode(List<int> permutation)
	{
		int n = permutation.Count;
		int index = 0;
		int factor = 1;
		for (int i = n - 1; i >= 0; i--)
		{
			int rank = permutation[i];
			index += rank * factor;
			factor *= Math.Max(1, (n - i));
		}
		return index;
	}

	private List<int> PermuradicDecode(int index, int size)
	{
		List<int> permutation = new List<int>(new int[size]);
		List<int> elements = Enumerable.Range(0, size).ToList();

		for (int i = size - 1; i >= 0; i--)
		{
			int factor = Factorial(i);
			int pos = (factor == 0) ? 0 : index / factor;
			permutation[size - 1 - i] = elements[pos];
			elements.RemoveAt(pos);
			index %= Math.Max(1, factor);
		}
		return permutation;
	}

	private int Factorial(int n)
	{
		int result = 1;
		for (int i = 2; i <= n; i++)
			result *= i;
		return result;
	}

	public void PrintBoard()
	{
		for (int i = 0; i < 8; i++)
		{
			string line = "";
			for (int j = 0; j < 8; j++)
			{
				line += board[i, j] + " ";
			}
			Debug.Log(line);
		}
	}
}