using System;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private int[] PieceTypeToValue = new int[] {0, 100, 300, 300, 500, 900, 10000}; // None, Pawn, Knight, Bishop, Rook, Queen, King

    private double[][] PiecePositionTable = new double[][]{
        // Pawn
        new double[]{0, 0, 0, 0, 0, 0, 0, 0,5, 5, 5, 5, 5, 5, 5, 5,1, 1, 2, 3, 3, 2, 1, 1,.5, .5, 1, 2.5, 2.5, 1, .5, .5,0, 0, 0, 2, 2, 0, 0, 0,.5, .5, -1, 0, 0, -1, -.5, .5,.5, 1, 1, 2, 2, 1, 1, .5,0, 0, 0, 0, 0, 0, 0, 0},
        // Knight
        new double[]{-5, 4, 3, 3, 3, 3, 4, -5,-4, 2, 0, 0, 0, 0, 2, -4,-3, 0, 1, 1.5, 1.5, 1, 0, -3,-3, .5, 1.5, 2, 2, 1.5, .5, -3,-3, 0, 1.5, 2, 2, 1.5, 0, -3,-3, .5, 1, 1.5, 1.5, 1, .5, -3,-4, 2, 0, .5, .5, 0, -2, -4,-5, 4, 3, 3, 3, 3, 4, -5},
        // Bishop
        new double[]{ -2, -1, -1, -1, -1, -1, -1, -2, -1, 0, 0, 0, 0, 0, 0, 1, -1, 0, .5, 1, 1, .5, 0, 1, -1, .5, .5, 1, 1, .5, .5, 1, -1, 0, 1, 1, 1, 1, 0, 1, -1, 1, 1, 1, 1, 1, 1, 1, -1, .5, 0, 0, 0, 0, .5, -1, -2, -1, -1, -1, -1, -1, -1, -2},
        // Rook
        new double[]{ 0, 0, 0, 0, 0, 0, 0, 0, .5, 1, 1, 1, 1, 1, 1, .5,-.5, 0, 0, 0, 0, 0, 0, -.5,-.5, 0, 0, 0, 0, 0, 0, -.5,-.5, 0, 0, 0, 0, 0, 0, -.5,-.5, 0, 0, 0, 0, 0, 0, -.5,-.5, 0, 0, 0, 0, 0, 0, -.5, 0, 0, 0, .5, .5, 0, 0, 0},
        // Queen
        new double[]{ -2, -1, 1, .5, -.5, -1, -1, -2,-1, 0, 0, 0, 0, 0, 0, 1,-1, 0, .5, .5, .5, .5, 0, 1,-.5, 0, .5, .5, .5, .5, 0, -.5, 0, 0, .5, .5, .5, .5, 0, -.5,-1, .5, .5, .5, .5, .5, 0, 1,-1, 0, .5, 0, 0, 0, 0, 1, -2, -1, -1, -.5, .5, -1, 1, -2},
        // King
        new double[]{ -3, 4, 4, 5, -5, -4, -4, -3, -3, 4, 4, -5, -5, 4, 4, -3, -3, 4, -4, -5, -5, 4, 4, -3, -3, 4, -4, -5, -5, -4, 4, -3, -2, -3, -3, -4, -4, -3, -3, -2,-1, 2, -2, -2, -2, -2, -2, 1, 2, 2, 0, 0, 0, 0, 2, 2 , 2, 3, 1, 0, 0, 1, 3, 2},
    };

    public Move Think(Board board, Timer timer)
    {
        var depth = 4;
        var random = new Random();
        var moves = board.GetLegalMoves();
        var move = moves[random.Next(moves.Length)];
        return Minimax(board, move, 0, depth, board.IsWhiteToMove).Item1;
    }

    private (Move, int) Minimax(Board board, Move lastMove, int lastValue, int depth, bool isMax) {
        // Depth check
        if (depth == 0) return (lastMove, BoardValue(board));

        Move[] legalMoves = board.GetLegalMoves();
        (Move, int) bestMove = (lastMove, isMax ? int.MinValue : int.MaxValue);

        foreach (Move legalMove in legalMoves) 
        {
            board.MakeMove(legalMove);
            // a-b pruning
            if ( !( ( isMax && BoardValue(board) >= lastValue ) || ( !isMax && BoardValue(board) <= lastValue ) ) ) 
            {
                board.UndoMove(legalMove);
                continue;
            }

            var next = Minimax(board, legalMove, BoardValue(board), depth-1, !isMax);
            if ( ( isMax && next.Item2 > bestMove.Item2 ) || ( !isMax && next.Item2 < bestMove.Item2 ) ) 
            {
                bestMove = (legalMove, next.Item2);
            }

            board.UndoMove(legalMove);
        }

        return bestMove;
    }

    /// <summary> Postive = White is better / Negative = Black is better </summary>
    private int BoardValue(Board board) 
    {
        int materialScore = 0;
        int mobilityScore = 0;
        foreach (var list in board.GetAllPieceLists()) 
        {
            foreach (var piece in list) 
            {
                double[] table = PiecePositionTable[(int)piece.PieceType-1];
                mobilityScore += (piece.IsWhite ? 1 : -1) * (int)(table[piece.Square.Index] * 2.0);
                materialScore += (piece.IsWhite ? 1 : -1) * PieceTypeToValue[(int)piece.PieceType];
            }
        }
        return materialScore + mobilityScore;
    }
}