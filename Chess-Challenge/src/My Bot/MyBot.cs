using System;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private double[] PieceTypeToValue = new double[] { 0, 100, 300, 300, 500, 900, 10000 }; // None, Pawn, Knight, Bishop, Rook, Queen, King

    // TODO: Mirroring / Compression?
    private double[][] PiecePositionTable = new double[][]{
        // Pawn
        mirror(new double[]
        {
            -3,  -3,  -3,  -3,
          -2.5,  -2,  -2,  -5,
          -2.5,  -3,  -4,  -3,
            -3,  -3,  -2, 1.5,
          -2.5,-2.5,  -1,   2,
            -2,  -2,   1,   2,
             4,   4,   4,   4,
            -3,  -3,  -3,  -3,
        }),
        // Knight
        mirror(new double[]
        {
            -5,  -4,  -3,  -3,
            -4,  -2,   1,   2,
            -3,   1,   3,   4,
            -3,   2,   4,   5,
            -3,   2,   4,   5,
            -3,   1,   3,   4,
            -4,  -2,   1,   2,
            -5,  -4,  -3,  -3
        }),
        // Bishop
        mirror(new double[]
        {
            -1,  -3,  -3,  -3,
            -3, 1.5,   0,   0,
            -3,   2, 1.5, 1.5,
            -3,  .5, 3.5,   2,
            -3,   1,   1,   2,
            -3,   0,   1,   2,
            -3,   0,   0,   0,
            -5,  -3,  -3,  -3
        }),
        // Rook
        mirror(new double[]
        {
            -2,  -2,   0,   1,
            -5,  -2,  -2,  -2,
            -5,  -2,  -2,  -2,
            -5,  -2,  -2,  -2,
            -5,  -2,  -2,  -2,
            -5,  -2,  -2,  -2,
           1.5,   4,   4,   4,
            .5,  .5,  .5,  .5
        }),
        // Queen
        mirror(new double[]
        {
            -4,  -2,  -2,  -1,
            -2,   1,   2,   3,
            -2,   2,   4,   5,
            -1,   3,   5,   5,
            -1,   3,   5,   5,
            -2,   2,   4,   5,
            -2,   1,   2,   3,
            -4,  -2,  -2,  -1
        }),
        // King
        new double[]
        {
             1,   1,   2,   0,   0,   0,   2,   1,
             1,  .5, -.5, -.5, -.5, -.5,  .5,   1,
             0,  -1,  -1,  -1,  -1,  -1,  -1,   0,
            -2,  -2,  -2,  -4,  -4,  -2,  -2,  -2,
            -2,  -3,  -3,  -5,  -5,  -3,  -3,  -2,
            -2,  -3,  -3,  -5,  -5,  -3,  -3,  -2,
            -3,  -4,  -4,  -5,  -5,  -4,  -3,  -3,
            -3,  -4,  -4,  -5,  -5,  -4,  -3,  -3
        }
    };

    public Move Think(Board board, Timer timer)
    {
        var depth = 4;
        var moves = board.GetLegalMoves();
        var move = moves[new Random().Next(moves.Length)];
        var chosen = Minimax(board, move, BoardvalueWithMove(board, move), depth, board.IsWhiteToMove);
        return chosen.Item1;
    }

    private (Move, double) Minimax(Board board, Move lastMove, double lastValue, int depth, bool isMax)
    {
        // Depth check
        if (depth == 0) return (lastMove, BoardValue(board));

        (Move, double) bestMove = (lastMove, isMax ? double.MinValue : double.MaxValue);

        foreach (Move legalMove in board.GetLegalMoves())
        {
            board.MakeMove(legalMove);
            // a-b pruning
            if (!((isMax && BoardValue(board) >= lastValue) || (!isMax && BoardValue(board) <= lastValue)))
            {
                board.UndoMove(legalMove);
                continue;
            }

            var next = Minimax(board, legalMove, BoardValue(board), depth - 1, !isMax);
            if ((isMax && next.Item2 > bestMove.Item2) || (!isMax && next.Item2 < bestMove.Item2))
            {
                bestMove = (legalMove, next.Item2);
            }

            board.UndoMove(legalMove);
        }

        return bestMove;
    }

    /// <summary> Postive = White is better / Negative = Black is better </summary>
    private double BoardValue(Board board)
    {
        double materialScore = 0;
        double positionScore = 0;
        foreach (var list in board.GetAllPieceLists())
        {
            foreach (var piece in list)
            {
                positionScore += (piece.IsWhite ? 1 : -1) * (PiecePositionTable[(int)piece.PieceType - 1][piece.IsWhite ? piece.Square.Index : 63 - piece.Square.Index]);
                materialScore += (piece.IsWhite ? 1 : -1) * PieceTypeToValue[(int)piece.PieceType];
            }
        }
        return materialScore
            + positionScore
            + (board.IsInCheckmate() ? (board.IsWhiteToMove ? -1 : 1) * 10000000 : 0)
            + (board.IsInCheck() ? (board.IsWhiteToMove ? -1 : 1) * 300 : 0)
            + (board.IsDraw() ? (board.IsWhiteToMove ? -1 : 1) * -100000 : 0);
    }

    private double BoardvalueWithMove(Board board, Move move)
    {
        board.MakeMove(move);
        var s = BoardValue(board);
        board.UndoMove(move);
        return s;
    }

    private static double[] mirror(double[] half)
    {
        var ret = new double[64];

        for (int i = 0; i < 32; i += 4)
        {
            Array.Copy(half, i, ret, i * 2, 4);
            Array.Reverse(half, i, 4);
            Array.Copy(half, i, ret, i * 2 + 4, 4);
        }

        return ret;
    }
}