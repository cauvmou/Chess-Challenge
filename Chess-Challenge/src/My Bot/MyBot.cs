using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{

    enum NodeBound
    {
        Upper,
        Lower,
    }

    class TranspositionTableEntry
    {
        public Move move;
        public int depth;
        public double value;
        public NodeBound? bound;

        public TranspositionTableEntry(Move move, int depth, double value, NodeBound? bound = null)
        {
            this.move = move;
            this.depth = depth;
            this.value = value;
            this.bound = bound;
        }
    }

    private Dictionary<ulong, TranspositionTableEntry> TranspositionTable = new Dictionary<ulong, TranspositionTableEntry>();

    private static readonly double[] PieceTypeToValue = { 0, 100, 350, 350, 525, 1000, 0 }; // None, Pawn, Knight, Bishop, Rook, Queen, King

    // TODO: Mirroring / Compression?
    private static int[][] PiecePositionTable = new int[][]{
        // Pawn
    mirror(new []
    {
        4,   4,   4,   4,
        5,   6,   6,   0,
        5,   4,   2,   4,
        4,   4,   6,  13,
        5,   5,   8,  14,
        6,   6,  12,  14,
       18,  18,  18,  18,
        4,   4,   4,   4,
    }),
    // Knight
    mirror(new []
    {
        0,   2,   4,   4,
        2,   6,  12,  14,
        4,  12,  16,  18,
        4,  14,  18,  20,
        4,  14,  18,  20,
        4,  12,  16,  18,
        2,   6,  12,  14,
        0,   2,   4,   4
    }),
    // Bishop
    mirror(new []
    {
        8,   4,   4,   4,
        4,  13,  10,  10,
        4,  14,  13,  13,
        4,  11,  17,  14,
        4,  12,  12,  14,
        4,  10,  12,  14,
        4,  10,  10,  10,
        0,   4,   4,   4
    }),
    // Rook
    mirror(new []
    {
        6,   6,  10,  12,
        0,   6,   6,   6,
        0,   6,   6,   6,
        0,   6,   6,   6,
        0,   6,   6,   6,
        0,   6,   6,   6,
       13,  18,  18,  18,
       11,  11,  11,  11
    }),
    // Queen
    mirror(new []
    {
        2,   6,   6,   8,
        6,  12,  14,  16,
        6,  14,  18,  20,
        8,  16,  20,  20,
        8,  16,  20,  20,
        6,  14,  18,  20,
        6,  12,  14,  16,
        2,   6,   6,   8
    }),
    // King
    new []
    {
         12,  12,  14,  10,  10,  10,  14,  12
    }.Concat(mirror(new []
    {
       12,  11,   9,   9,
       10,   8,   8,   8,
        6,   6,   6,   2,
        6,   4,   4,   0,
        6,   4,   4,   0,
        4,   2,   2,   0,
        4,   2,   2,   0,
       10,  10,  10,  10,  // TODO: trim this?
    })).ToArray()
    };

    private int depth = 4;

    public Move Think(Board board, Timer timer)
    {
        System.Console.WriteLine($"TableSize: {TranspositionTable.Count}");
        // depthIncreaseCheck(board);
        var move = Search(board, double.NegativeInfinity, double.PositiveInfinity, 4, 2, board.IsWhiteToMove).Item1;
        return move;
    }

    /// <summary> AlphaNegamax </summary>
    private (Move, double) Search(Board board, double alpha, double beta, int depth, int extensions, bool isWhite)
    {
        var alphaCopy = alpha;
        if (TranspositionTable.ContainsKey(board.ZobristKey))
        {
            var entry = TranspositionTable.GetValueOrDefault(board.ZobristKey);
            if (entry.depth >= depth)
            {
                if (entry.bound == null) return (entry.move, entry.value);
                else if (entry.bound == NodeBound.Lower) alpha = Math.Max(alpha, entry.value);
                else if (entry.bound == NodeBound.Upper) beta = Math.Min(beta, entry.value);

                if (alpha >= beta) return (entry.move, entry.value);
            }
        }

        var moves = board.GetLegalMoves();

        // Depth check
        if (depth == 0 || board.IsInCheckmate() || board.IsDraw()) return (Move.NullMove, Quiescence(board, alpha, beta, isWhite, extensions));

        (Move, double) bestValue = (moves[0], double.NegativeInfinity);

        foreach (Move legalMove in moves)
        {
            board.MakeMove(legalMove);

            var score = -Search(board, -beta, -alpha, depth - 1, extensions, !isWhite).Item2;
            if (score >= beta)
            {
                bestValue = (legalMove, score);
                board.UndoMove(legalMove);
                break;
            }
            if (score > bestValue.Item2)
            {
                bestValue = (legalMove, score);
                if (score > alpha) alpha = score;
            }
            board.UndoMove(legalMove);
        }

        var value = bestValue.Item2;
        var tableEntry = new TranspositionTableEntry(bestValue.Item1, depth, bestValue.Item2, value <= alphaCopy ? NodeBound.Upper : value >= beta ? NodeBound.Lower : null);
        if (TranspositionTable.ContainsKey(board.ZobristKey)) TranspositionTable[board.ZobristKey] = tableEntry;
        else TranspositionTable.Add(board.ZobristKey, tableEntry);

        return bestValue;
    }

    private double Quiescence(Board board, double alpha, double beta, bool isWhite, int depth)
    {
        var best = Evaluate(board) * (isWhite ? 1 : -1);
        if (best >= beta) return beta;

        if (depth > 0) foreach (Move capture in board.GetLegalMoves(true))
            {
                board.MakeMove(capture);
                var score = -Quiescence(board, -beta, -Math.Max(alpha, best), isWhite, depth - 1);
                board.UndoMove(capture);
                best = Math.Max(score, best);

                if (best >= beta) break;
            }

        return best;
    }

    /// <summary> Postive = White is better / Negative = Black is better </summary>
    private static double Evaluate(Board board)
    {
        double materialScore = 0;
        double positionScore = 0;
        foreach (var list in board.GetAllPieceLists())
        {
            foreach (var piece in list)
            {
                double multiplier = piece.IsWhite ? 1 : -1;
                materialScore += multiplier * PieceTypeToValue[(int)piece.PieceType];
                positionScore += multiplier * (PiecePositionTable[(int)piece.PieceType - 1][piece.IsWhite ? piece.Square.Index : 63 - piece.Square.Index] / 2 - 5);
            }
        }
        return materialScore
            + positionScore
            + (board.IsInCheckmate() ? board.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity : 0.0)
            //+ (board.IsInCheck() ? (board.IsWhiteToMove ? -1 : 1) * 300 : 0)
            + (board.IsDraw() ? (board.IsWhiteToMove ? -1 : 1) * -10000 : 0.0);
    }

    private static int[] mirror(int[] half)
    {
        var ret = new int[64];

        for (int i = 0; i < 32; i += 4)
        {
            Array.Copy(half, i, ret, i * 2, 4);
            Array.Reverse(half, i, 4);
            Array.Copy(half, i, ret, i * 2 + 4, 4);
        }

        return ret;
    }

    // delete this if bot is slow AND stupid 
    // HE WAS!
    // public void depthIncreaseCheck(Board board)
    // {
    //     depth = (board.GetLegalMoves().Length) switch
    //     {
    //         > 20 => 4,
    //         > 10 => 5,
    //         <= 10 => 6
    //     };
    // }
}