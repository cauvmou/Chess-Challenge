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

    private static readonly double[] PieceTypeToValue = { 0, 82, 337, 365, 477, 1025, 0 }; // None, Pawn, Knight, Bishop, Rook, Queen, King

    // TODO: Mirroring / Compression?
    // Holds ranks (ordered 7-0) that are ulongs in which there are the files (ordered 0-7) encoded.
    private static ulong[][] PiecePositionTable = new ulong[][]{
        // Pawn
        new ulong[]{ 0x0, 0x1919191919191919, 0x5050a0f0f0a0505, 0x202050c0c050202, 0xa0a000000, 0x2fefb0000fbfe02, 0x20505f6f6050502, 0x0 },
        // Knight
        new ulong[]{ 0xe7ecf1f1f1f1ece7, 0xecf600000000f6ec, 0xf1000507070500f1, 0xf102070a0a0702f1, 0xf100070a0a0700f1, 0xf1020507070502f1, 0xecf600020200f6ec, 0xe7ecf1f1f1f1ece7 },
        // Bishop
        new ulong[]{ 0xf6fbfbfbfbfbfbf6, 0xfb000000000000fb, 0xfb000205050200fb, 0xfb020205050202fb, 0xfb000505050500fb, 0xfb050505050505fb, 0xfb020000000002fb, 0xf6fbfbfbfbfbfbf6 },
        // Rook
        new ulong[]{ 0x0, 0x205050505050502, 0xfe000000000000fe, 0xfe000000000000fe, 0xfe000000000000fe, 0xfe000000000000fe, 0xfe000000000000fe, 0x202000000 },
        // Queen
        new ulong[]{ 0xf6fbfbfefefbfbf6, 0xfb000000000000fb, 0xfb000202020200fb, 0xfe000202020200fe, 0xfe00020202020000, 0xfb000202020202fb, 0xfb000000000200fb, 0xf6fbfbfefefbfbf6 },
        // King
        new ulong[]{ 0xf1ecece7e7ececf1, 0xf1ecece7e7ececf1, 0xf1ecece7e7ececf1, 0xf1ecece7e7ececf1, 0xf6f1f1ececf1f1f6, 0xfbf6f6f6f6f6f6fb, 0xa0a000000000a0a, 0xa0f050000050f0a },
        // king endgame
        new ulong[]{ 0xe7ecf1f6f6f1ece7, 0xf1f6fb0000fbf6f1, 0xf1fb0a0f0f0afbf1, 0xf1fb0f14140ffbf1, 0xf1fb0f14140ffbf1, 0xf1fb0a0f0f0afbf1, 0xf1f100000000f1f1, 0xe7f1f1f1f1f1f1e7 }
    };
    private static double PositionMultiplier = 0.6;

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
        if (board.IsInCheckmate())
        {
            return board.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity;
        }
        else if (board.IsDraw())
        {
            return 0;
        }

        double materialScore = 0;
        double positionScore = 0;
        foreach (var list in board.GetAllPieceLists())
        {
            foreach (var piece in list)
            {
                double multiplier = piece.IsWhite ? 1 : -1;
                materialScore += multiplier * PieceTypeToValue[(int)piece.PieceType];
                var square = new Square(piece.Square.File, (piece.IsWhite ? 7 - piece.Square.Rank : piece.Square.Rank));
                positionScore += multiplier * ((double)(sbyte)BitConverter.GetBytes(PiecePositionTable[(int)piece.PieceType - 1][square.Rank])[square.File]) * 2.0 * PositionMultiplier;
            }
        }


        return materialScore + positionScore;
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