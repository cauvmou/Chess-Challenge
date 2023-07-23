using System;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private int[] PieceTypeToValue = new int[] {0, 1, 3, 3, 5, 9, int.MaxValue}; // None, Pawn, Knight, Bishop, Rook, Queen, King
    public Move Think(Board board, Timer timer)
    {
        var depth = 4;
        System.Console.WriteLine("Current Board Value: " + BoardValue(board));
        return board.IsWhiteToMove ? Maxi(board, new Move(), int.MinValue, depth).Item1 : Mini(board, new Move(), int.MaxValue, depth).Item1;
    }

    private (Move, int) Maxi(Board board, Move lastMove, int lastValue, int depth) {
        if (depth == 0) 
            return (lastMove, BoardValue(board));
        (Move, int) max = (lastMove, int.MinValue);
        foreach (Move move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            if (lastValue <= BoardValue(board))
            {
                var next = Mini(board, move, BoardValue(board), depth-1);
                if (next.Item2 >= max.Item2) {
                    max = (move, next.Item2);
                }
            }
            board.UndoMove(move);
        }
        return max;
    }

    private (Move, int) Mini(Board board, Move lastMove, int lastValue, int depth) {
        if (depth == 0) 
            return (lastMove, BoardValue(board));
        (Move, int) min = (lastMove, int.MaxValue);
        foreach (Move move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            if (lastValue >= BoardValue(board))
            {
                var next = Maxi(board, move, BoardValue(board), depth-1);
                if (next.Item2 <= min.Item2) {
                    min = (move, next.Item2);
                }
            }
            board.UndoMove(move);
        }
        return min;
    }

    /// <summary> Postive = White is better / Negative = Black is better </summary>
    private int BoardValue(Board board) 
    {
        int value = 0;
        foreach (var list in board.GetAllPieceLists()) 
        {
            foreach (var piece in list) 
            {
                value += (piece.IsWhite ? 1 : -1) * PieceTypeToValue[(int)piece.PieceType];
            }
        }
        return value;
    }
}