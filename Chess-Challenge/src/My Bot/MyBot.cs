using System;
using System.Numerics;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    // int[] pieceValues = { 0, 1, 3, 3, 5, 9, 20 };
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

    public void ExtractValues(ulong pack) {
        byte[] vals = new byte[64*64];
        byte first = 0;
        var x = pack << 56;
        
        var first2 = (ulong) first | x;
    }

    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        // Console.WriteLine(BoardValue(board), true);

        ulong[] bestValue = { 
            
            6143424123412342134, 314324123412342134, 314324123412342134, 
            314324123412342134, 314324123412342134, 314324123412342134, 
            314324123412342134, 314324123412342134, 314324123412342134, 
            314324123412342134, 314324123412342134, 314324123412342134, 
            314324123412342134, 314324123412342134, 314324123412342134 
        };
        
        Move? bestMove = null;
    
        MiniMax(board, 4, 0, ref bestMove, board.IsWhiteToMove);
        return bestMove??moves[0];
    }

    public int MiniMax(Board board, int depth, int currentDepth, ref Move? bestMove, bool isMax)
    {
        if (currentDepth == depth)
        {
            return BoardValue(board);
        }
        if (isMax) {
            int value = int.MinValue;
            foreach (Move move in board.GetLegalMoves())
            {
                board.MakeMove(move);
                int score = MiniMax(board, depth, currentDepth + 1, ref bestMove, !isMax);
                if (score > value) 
                {
                    value = score;
                    if (currentDepth == 0) 
                    {
                        bestMove = move;
                    }
                        
                }
                board.UndoMove(move);
            }
            return value;
        } else {
            int value = int.MaxValue;
            foreach (Move move in board.GetLegalMoves())
            {
                board.MakeMove(move);
                int score = MiniMax(board, depth, currentDepth + 1, ref bestMove, !isMax);
                if (score < value) 
                {
                    value = score;
                    if (currentDepth == 0) 
                    {
                        bestMove = move;
                    }
                }
                board.UndoMove(move);
            }
            return value;
        }
    }

    public int BoardValue(Board board)
    {
        int value = 0;
        foreach (PieceList pieceList in board.GetAllPieceLists())
        {
            foreach (Piece piece in pieceList)
            {
                value += pieceValues[(int)piece.PieceType] * (piece.IsWhite ? 1 : -1);
            }
        }
        return value;
    }

}