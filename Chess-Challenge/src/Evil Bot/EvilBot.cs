using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;


namespace ChessChallenge.Example
{
    public class EvilBot : IChessBot
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
        private static double[][] PiecePositionTable = new double[][]{
        // Pawn
        mirror(new double[]
        {
            2,   2,   2,   2,
            2.5,  3,   3,   0,
            2.5,  2,   1,   2,
            2,   2,   3, 6.5,
            2.5, 2.5,  4,   7,
            3,   3,   6,   7,
            9,   9,   9,   9,
            2,   2,   2,   2,
        }),
        // Knight
        mirror(new double[]
        {
            0,   1,   2,   2,
            1,   3,   6,   7,
            2,   6,   8,   9,
            2,   7,   9,  10,
            2,   7,   9,  10,
            2,   6,   8,   9,
            1,   3,   6,   7,
            0,   1,   2,   2
        }),
        // Bishop
        mirror(new double[]
        {
            4,   2,   2,   2,
            2,  6.5,  5,   5,
            2,   7,  6.5, 6.5,
            2,  5.5,  8.5,  7,
            2,   6,   6,   7,
            2,   5,   6,   7,
            2,   5,   5,   5,
            0,   2,   2,   2
        }),
        // Rook
        mirror(new double[]
        {
            3,   3,   5,   6,
            0,   3,   3,   3,
            0,   3,   3,   3,
            0,   3,   3,   3,
            0,   3,   3,   3,
            0,   3,   3,   3,
            6.5,   9,   9,   9,
            5.5,  5.5,  5.5,  5.5
        }),
        // Queen
        mirror(new double[]
        {
            1,   3,   3,   4,
            3,   6,   7,   8,
            3,   7,   9,  10,
            4,   8,  10,  10,
            4,   8,  10,  10,
            3,   7,   9,  10,
            3,   6,   7,   8,
            1,   3,   3,   4
        }),
        // King
        new double[]
        {
             6,   6,   7,   5,   5,   5,   7,   6
        }.Concat(mirror(new double[]
        {
             6, 5.5, 4.5, 4.5,
             5,   4,   4,   4,
             3,   3,   3,   1,
             3,   2,   2,   0,
             3,   2,   2,   0,
             2,   1,   1,   0,
             2,   1,   1,   0,
             5,   5,   5,   5,  // TODO: trim this?
        })).ToArray()
    };

        public Move Think(Board board, Timer timer)
        {
            System.Console.WriteLine($"TableSize: {TranspositionTable.Count}");
            var move = Search(board, double.NegativeInfinity, double.PositiveInfinity, 4, 2, board.IsWhiteToMove).Item1;
            return move.IsNull ? board.GetLegalMoves()[new Random().Next(board.GetLegalMoves().Length)] : move;
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
            if (board.IsInCheckmate() || board.IsDraw()) return (Move.NullMove, Quiescence(board, alpha, beta, isWhite, 0));
            if (depth == 0) return (Move.NullMove, Quiescence(board, alpha, beta, isWhite, extensions));

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
                    if (score > alpha)
                    {
                        alpha = score;
                    }
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
                    positionScore += multiplier * (PiecePositionTable[(int)piece.PieceType - 1][piece.IsWhite ? piece.Square.Index : 63 - piece.Square.Index] - 5);
                }
            }
            return materialScore
                + positionScore
                + (board.IsInCheckmate() ? board.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity : 0.0)
                //+ (board.IsInCheck() ? (board.IsWhiteToMove ? -1 : 1) * 300 : 0)
                + (board.IsDraw() ? (board.IsWhiteToMove ? -1 : 1) * -10000 : 0.0);
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
}