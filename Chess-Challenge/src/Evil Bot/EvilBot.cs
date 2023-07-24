/*using ChessChallenge.API;
using System;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

        public Move Think(Board board, Timer timer)
        {
            Move[] allMoves = board.GetLegalMoves();

            // Pick a random move to play if nothing better is found
            Random rng = new();
            Move moveToPlay = allMoves[rng.Next(allMoves.Length)];
            int highestValueCapture = 0;

            foreach (Move move in allMoves)
            {
                // Always play checkmate in one
                if (MoveIsCheckmate(board, move))
                {
                    moveToPlay = move;
                    break;
                }

                // Find highest value capture
                Piece capturedPiece = board.GetPiece(move.TargetSquare);
                int capturedPieceValue = pieceValues[(int)capturedPiece.PieceType];

                if (capturedPieceValue > highestValueCapture)
                {
                    moveToPlay = move;
                    highestValueCapture = capturedPieceValue;
                }
            }

            return moveToPlay;
        }

        // Test if this move gives checkmate
        bool MoveIsCheckmate(Board board, Move move)
        {
            board.MakeMove(move);
            bool isMate = board.IsInCheckmate();
            board.UndoMove(move);
            return isMate;
        }
    }
}*/
using System;
using System.Collections.Generic;
using ChessChallenge.API;
namespace ChessChallenge.Example {
    public class EvilBot : IChessBot
    {
        private double[] PieceTypeToValue = new double[] {0, 100, 300, 300, 500, 900, 10000}; // None, Pawn, Knight, Bishop, Rook, Queen, King

        // TODO: Mirroring / Compression?
        private double[][] PiecePositionTable = new double[][]{
            // Pawn
            new double[]{0, 0, 0, 0, 0, 0, 0, 0, 5, 5, 5, 5, 5, 5, 5, 5,1, 1, 2, 3, 3, 2, 1, 1,.5, .5, 1, 2.5, 2.5, 1, .5, .5,0, 0, 0, 2, 2, 0, 0, 0,.5, .5, -1, 0, 0, -1, -.5, .5,.5, 1, 1, 2, 2, 1, 1, .5,0, 0, 0, 0, 0, 0, 0, 0},
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
            var moves = board.GetLegalMoves();
            var move = moves[new Random().Next(moves.Length)];
            var chosen = Minimax(board, move, BoardvalueWithMove(board, move), depth, board.IsWhiteToMove);
            return chosen.Item1;
        }

        private (Move, double) Minimax(Board board, Move lastMove, double lastValue, int depth, bool isMax) {
            // Depth check
            if (depth == 0) return (lastMove, BoardValue(board));

            Move[] legalMoves = board.GetLegalMoves();
            (Move, double) bestMove = (lastMove, lastValue);

            foreach (Move legalMove in legalMoves) 
            {
                board.MakeMove(legalMove);
                // a-b pruning
                if ( !( ( isMax && BoardValue(board) > lastValue ) || ( !isMax && BoardValue(board) < lastValue ) ) ) 
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
        private double BoardValue(Board board) 
        {
            double materialScore = 0;
            double positionScore = 0;
            foreach (var list in board.GetAllPieceLists()) 
            {
                foreach (var piece in list) 
                {
                    positionScore += (piece.IsWhite ? 1 : -1) * (PiecePositionTable[(int)piece.PieceType-1][piece.IsWhite ? 63 - piece.Square.Index : piece.Square.Index]);
                    materialScore += (piece.IsWhite ? 1 : -1) * PieceTypeToValue[(int)piece.PieceType];
                }
            }
            return materialScore 
                + positionScore
                + (board.IsInCheckmate() ? (board.IsWhiteToMove ? -1 : 1) * 10000000 : 0) 
                + (board.IsInCheck() ? (board.IsWhiteToMove ? -1 : 1) * 300 : 0)
                + (board.IsDraw() ? (board.IsWhiteToMove ? -1 : 1) * -100000 : 0);
        }

        private double BoardvalueWithMove(Board board, Move move) {
            board.MakeMove(move);
            var s = BoardValue(board);
            board.UndoMove(move);
            return s;
        }
    }
}
