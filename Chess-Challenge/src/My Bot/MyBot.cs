using System;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    const sbyte flagInvalid = 0, flagUpper = 1, flagLower = 2, flagExact = 3;

    struct TranspositionTableEntry
    {
        public Move move;
        public sbyte depth, flag;
        public int value;
        public ulong fullKey;
    }
    private static ulong TpMask = 0x7FFFFF;
    private TranspositionTableEntry[] TranspositionTable;
    private static int[] PieceToPhaseValue = { 0, 0, 1, 1, 2, 4, 0 }, PieceTypeToValue = { 0, 82, 337, 365, 477, 1025, 0 }; // None, Pawn, Knight, Bishop, Rook, Queen, King

    // Holds ranks (ordered 7-0) that are ulongs in which there are the files (ordered 0-7) encoded.
    private static ulong[,] PiecePositionTable = new ulong[,]{
        { 0x0, 0xfb113f222f1e4331, 0xf60c1c200f0d03fd, 0xf508060b0a0306f9, 0xf405030806fefff3, 0xfa100101fbfefef3, 0xf5130cf9f5f600ef, 0x0 },                               // Pawn MG
        { 0xcbf9d01ee8efd4ad, 0xf8031f0b1224ecdc, 0x1624402a20121ee9, 0xb0922121a0908fc, 0xfc0a090e060802fa, 0xf80c08090506fcf5, 0xf7f90900fffae6f2, 0xf5f7f2f8f0e3f6cc },  // Knight MG
        { 0xfc03ebf4eed702f2, 0xe9091d0ffaf708f3, 0xff121911141512f8, 0xff031212190902fe, 0x20506110d0606fd, 0x5090d0707070700, 0x100a0300080702, 0xf6edfafaf6f9fff0 },     // Bishop MG
        { 0x150f041f19101510, 0x160d21281f1d100d, 0x81e1608120d09fe, 0xf6fc110c0d03fbf4, 0xf503fd0400faf3ee, 0xf0fe0001f8f8f4ea, 0xddfd0500fcf6f8ea, 0xf3ee03080800faf7 },  // Rook MG
        { 0x1615161d060e00f2, 0x1b0e1cf800feedf4, 0x1c171c0e0403f8fa, 0xff0800f8f8f3f3, 0xff01fefffbfcf3fc, 0x20701fefffb01f9, 0xff07040105fcef, 0xe7f1f4f905fcf700 },      // Queen MG
        { 0x601efe4f9080be0, 0xf2edfefcfdf6000e, 0xf50b03f6f8010cfc, 0xeef9f4f1f3faf6f8, 0xe7f0eae9edf300e8, 0xf3f9f1eae9f5f9f9, 0x404f8ebe0fc0300, 0x70cf204e50612f9 },    // King MG
        { 0x0, 0x5d524249434f5659, 0x2a291a1c212a322f, 0x80802ff02060c10, 0x1fcfdfdff0406, 0xfc00fe0000fd0302, 0xfd01000605040406, 0x0 },                                   // Pawn EG
        { 0xcfe1f3f1f2faede3, 0xe6f4f4fcfff4fcf4, 0xecf7fc000405f6f4, 0xf704050b0b0b01f8, 0xf70208080c08fdf7, 0xf5f6ff050700fff5, 0xeaf5f6fffefbf6eb, 0xe0e7f7f5f9f5e7f2 }, // Knight EG
        { 0xf4f8fcfdfcfbf6f9, 0xf9fefafffa03fefc, 0x20003ff0000fc01, 0x1010507040604ff, 0xfcff0503090601fd, 0xf9fd01060504fffa, 0xf3f9fc0200fdf7f9, 0xf8fef8fcfef5fcf5 },   // Bishop EG
        { 0x204060607090506, 0x10401ff05060605, 0xfffeff0202030303, 0x100000100060102, 0xfbfcfdfe02040201, 0xf8fcfafd00fe00fe, 0xfffbfcfc0100fdfd, 0xf602fafe000101fc },    // Rook EG
        { 0xa05090d0d0b0bfc, 0xf0c1d14100af8, 0x4091117180403f6, 0x121c141c160c0b01, 0xb13110f17090ef7, 0x20508040307f3f8, 0xf0eef5f8f8f1f5f5, 0xecf6f0feebf5f2f0 },        // Queen EG
        { 0xf80207fbf7f7efdb, 0x50b1308080708fa, 0x616160a070b0805, 0x10d100d0d0c0bfc, 0xfb040b0d0c0afef7, 0xfc03080b0a05fff7, 0xf8fe02070602fbf3, 0xebf4f9f2fbf6efe6 }     // King EG
    };
    private static int CheckmateValue = 1000000;
    private static int DrawReluctancyValue = -20;
    private Board Board;
    private Move BestMove;
    private int StartPly;

    public MyBot()
    {
        TranspositionTable = new TranspositionTableEntry[TpMask + 1];
    }

    public Move Think(Board board, Timer timer)
    {
        Board = board;
        StartPly = Board.PlyCount;
        int allocatedTime = timer.MillisecondsRemaining / 2000;
        int reachedDepth = 0;
        for (int depth = 4; depth < 128; depth++)
        {
            Search(-CheckmateValue, CheckmateValue, depth, isRoot: true);
            reachedDepth = depth;
            if (timer.MillisecondsElapsedThisTurn > allocatedTime) break;
        }
        return BestMove;
    }

    /// <summary> AlphaNegamax </summary>
    private int Search(int alpha, int beta, int depth, bool isRoot = false)
    {
        ref TranspositionTableEntry transposition = ref TranspositionTable[Board.ZobristKey & TpMask];
        int alphaCopy = alpha, transValue = transposition.value;

        if (transposition.flag != flagInvalid && transposition.depth >= depth && !isRoot)
        {
            switch (transposition.flag)
            {
                case flagExact: return transValue;
                case flagLower: alpha = Math.Max(alpha, transValue); break;
                case flagUpper: beta = Math.Min(beta, transValue); break;
            }

            if (alpha >= beta) return transValue;
        }

        // Leaf Nodes
        if (Board.IsInCheckmate()) return -CheckmateValue + Board.PlyCount - StartPly;
        if (Board.IsDraw()) return DrawReluctancyValue;

        bool quiescence = depth <= 0;
        int bestValue = -CheckmateValue;

        // Delta Pruning
        if (quiescence)
        {
            bestValue = Evaluate() * (Board.IsWhiteToMove ? 1 : -1);
            if (bestValue >= beta) return beta;
            if (bestValue < alpha - PieceTypeToValue[5]) return alpha;
            alpha = Math.Max(alpha, bestValue);
        }

        var moves = OrderMoves(Board.GetLegalMoves(quiescence), transposition);

        foreach (Move legalMove in moves)
        {
            Board.MakeMove(legalMove);
            var score = -Search(-beta, -alpha, depth - 1);
            Board.UndoMove(legalMove);
            if (score >= beta)
            {
                bestValue = score;
                transposition.move = legalMove;
                if (isRoot) BestMove = legalMove;
                break;
            }
            if (score > bestValue)
            {
                bestValue = score;
                transposition.move = legalMove;
                if (isRoot) BestMove = legalMove;
                if (score > alpha) alpha = score;
            }
        }

        if (!quiescence && moves.Length == 0) { return Board.IsInCheck() ? -CheckmateValue + Board.PlyCount - StartPly : 0; }

        transposition.depth = (sbyte)depth;
        transposition.value = bestValue;
        transposition.flag = bestValue <= alphaCopy ? flagUpper : bestValue >= beta ? flagLower : flagExact;
        transposition.fullKey = Board.ZobristKey;

        return bestValue;
    }

    private Move[] OrderMoves(Move[] moves, TranspositionTableEntry transposition)
    {
        int[] movePriorities = new int[moves.Length];
        for (int m = 0; m < moves.Length; m++) movePriorities[m] = MoveScore(moves[m], transposition);
        Array.Sort(movePriorities, moves);
        Array.Reverse(moves);
        return moves;
    }

    private int MoveScore(Move move, TranspositionTableEntry transposition)
    {
        int score = 0;
        if (transposition.move == move && transposition.fullKey == Board.ZobristKey) score += 10000;
        if (move.IsCapture) score += PieceTypeToValue[(int)move.CapturePieceType] - (move.MovePieceType == PieceType.King ? CheckmateValue : PieceTypeToValue[(int)move.MovePieceType]);
        if (move.IsPromotion) score += PieceTypeToValue[(int)move.PromotionPieceType];
        return score;
    }

    /// <summary> Postive = White is better / Negative = Black is better </summary>
    private int Evaluate()
    {
        if (Board.IsInCheckmate()) return Board.IsWhiteToMove ? -CheckmateValue : CheckmateValue;
        //else if (board.IsDraw()) return drawReluctancy(board)*100;
        else if (Board.IsDraw()) return 0;

        int midGame = 0,
            endGame = 0;
        int phase = 24; // phase = TotalPiecePhaseValue

        foreach (var list in Board.GetAllPieceLists())
        {
            foreach (var piece in list)
            {
                int multiplier = piece.IsWhite ? 1 : -1,
                    material = multiplier * PieceTypeToValue[(int)piece.PieceType];
                Square square = new Square(piece.Square.File, (piece.IsWhite ? 7 - piece.Square.Rank : piece.Square.Rank));
                midGame += boardEval(piece, square, material, 1, multiplier);
                endGame += boardEval(piece, square, material, -5, multiplier);
                phase -= PieceToPhaseValue[(int)piece.PieceType];
            }
        }

        phase = ((phase << 8) + 12) / 24; // phase = (phase * 256 + (TotalPiecePhaseValue / 2)) / TotalPiecePhaseValue
        return ((midGame * (256 - phase)) + (endGame * phase)) / 256;
    }

    private int boardEval(Piece piece, Square square, int material, int pieceTypeShift, int multiplier)
    {
        return multiplier * (((sbyte)BitConverter.GetBytes(PiecePositionTable[(int)piece.PieceType - pieceTypeShift, square.Rank])[square.File]) << 1) + material;
    }
}