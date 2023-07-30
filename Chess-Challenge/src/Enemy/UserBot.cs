using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ChessChallenge.API;

namespace ChessChallenge.Enemy
{
    //#define LOGGING
    //#define VISUALIZER

    using ChessChallenge.API;
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public class UserBot : IChessBot
    {
        //PieceType[] pieceTypes    = { PieceType.None, PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen, PieceType.King};
        private readonly int[] k_pieceValues = { 0, 100, 310, 330, 500, 900, 20000 };

        int[] piecePhase = { 0, 0, 1, 1, 2, 4, 0 };
        ulong[] psts = {
    657614902731556116, 420894446315227099, 384592972471695068, 312245244820264086,
    364876803783607569, 366006824779723922, 366006826859316500, 786039115310605588,
    421220596516513823, 366011295806342421, 366006826859316436, 366006896669578452,
    162218943720801556, 440575073001255824, 657087419459913430, 402634039558223453,
    347425219986941203, 365698755348489557, 311382605788951956, 147850316371514514,
    329107007234708689, 402598430990222677, 402611905376114006, 329415149680141460,
    257053881053295759, 291134268204721362, 492947507967247313, 367159395376767958,
    384021229732455700, 384307098409076181, 402035762391246293, 328847661003244824,
    365712019230110867, 366002427738801364, 384307168185238804, 347996828560606484,
    329692156834174227, 365439338182165780, 386018218798040211, 456959123538409047,
    347157285952386452, 365711880701965780, 365997890021704981, 221896035722130452,
    384289231362147538, 384307167128540502, 366006826859320596, 366006826876093716,
    366002360093332756, 366006824694793492, 347992428333053139, 457508666683233428,
    329723156783776785, 329401687190893908, 366002356855326100, 366288301819245844,
    329978030930875600, 420621693221156179, 422042614449657239, 384602117564867863,
    419505151144195476, 366274972473194070, 329406075454444949, 275354286769374224,
    366855645423297932, 329991151972070674, 311105941360174354, 256772197720318995,
    365993560693875923, 258219435335676691, 383730812414424149, 384601907111998612,
    401758895947998613, 420612834953622999, 402607438610388375, 329978099633296596,
    67159620133902};

        //private const byte INVALID = 0, EXACT = 1, LOWERBOUND = 2, UPPERBOUND = 3;

        //14 bytes per entry, likely will align to 16 bytes due to padding (if it aligns to 32, recalculate max TP table size)
        struct Transposition
        {
            public ulong zobristHash;
            public Move move;
            public int evaluation;
            public sbyte depth;
            public byte flag;
        };

        Move[] m_killerMoves;

        Transposition[] m_TPTable;

        Board m_board;

#if LOGGING
    private int m_evals = 0;
    private int m_nodes = 0;
#endif


        public UserBot()
        {
            m_killerMoves = new Move[1024];
            m_TPTable = new Transposition[0x800000];
        }

        public Move Think(Board board, Timer timer)
        {
#if LOGGING
            if(board.GameMoveHistory.Length > 0) Console.WriteLine("Opponent played {0}", board.GameMoveHistory.Last().ToString());
#endif

#if VISUALIZER
            BitboardHelper.VisualizeBitboard(GetBoardControl(PieceType.Pawn, !board.IsWhiteToMove));
#endif
            m_board = board;
#if LOGGING
        Console.WriteLine(board.GetFenString());
#endif
            Transposition bestMove = m_TPTable[board.ZobristKey & 0x7FFFFF];
            int maxTime = timer.MillisecondsRemaining / 30;
            for (sbyte depth = 1; ; depth++)
            {
#if LOGGING
                m_evals = 0;
                m_nodes = 0;
#endif
                Search(depth, -100000000, 100000000);
                bestMove = m_TPTable[board.ZobristKey & 0x7FFFFF];
#if LOGGING
                Console.WriteLine("Depth: {0,2} | Nodes: {1,10} | Evals: {2,10} | Time: {3,5} Milliseconds | Best {4} | Eval: {5}", depth, m_nodes, m_evals, timer.MillisecondsElapsedThisTurn, bestMove.move, bestMove.evaluation);
#endif
                if (!ShouldExecuteNextDepth(timer, maxTime)) break;
            }
#if LOGGING
            Console.Write("PV: ");
        PrintPV(board, 20);
#endif
            return bestMove.move;
        }

        int Search(int depth, int alpha, int beta)
        {
#if LOGGING
        m_nodes++;
#endif

            bool inQSearch = depth <= 0;
            bool pvNode = beta > alpha + 1;
            int bestEvaluation = -2147483647;
            int startingAlpha = alpha;

            //PVS stuff
            if (!pvNode && !inQSearch) beta = alpha--;

            //See if we've checked this board state before
            ref Transposition transposition = ref m_TPTable[m_board.ZobristKey & 0x7FFFFF];
            if (transposition.zobristHash == m_board.ZobristKey && transposition.depth >= depth)
            {
                //If we have an "exact" score (a < score < beta) just use that
                if (transposition.flag == 1) return transposition.evaluation;
                //If we have a lower bound better than beta, use that
                if (transposition.flag == 2 && transposition.evaluation >= beta) return transposition.evaluation;
                //If we have an upper bound worse than alpha, use that
                if (transposition.flag == 3 && transposition.evaluation <= alpha) return transposition.evaluation;
            }

            //Leaf node conditions
            if (m_board.IsDraw()) return -10;
            if (m_board.IsInCheckmate()) return m_board.PlyCount - 100000000;

            int standingPat = Evaluate();

            //RFP implementation
            if (!pvNode && !inQSearch && depth <= 1 && !m_board.IsInCheck() && standingPat >= beta + 2000 * depth) return standingPat;

            //QSearch: leaf none & standing-pat implementation
            //Get all moves if in check or not in QSearch, otherwise get all moves
            Move[] moves = m_board.GetLegalMoves(inQSearch && !m_board.IsInCheck());
            if (moves.Length == 0) return standingPat; //Can't get here in checkmate (see above)
            if (inQSearch)
            {
                if (standingPat >= beta) return beta;
                if (standingPat > alpha) alpha = standingPat;
            }

            OrderMoves(ref moves, depth);

            //Assume first move is best move in position
            Move bestMove = moves[0];

            for (int m = 0; m < moves.Length; m++)
            {
                m_board.MakeMove(moves[m]);
                int evaluation = -Search(depth - 1, (m == 0) ? -beta : -alpha - 1, -alpha);
                if (m != 0 && evaluation > alpha && evaluation < beta)
                    evaluation = -Search(depth - 1, -beta, -alpha);  //Use full window if null window was good
                m_board.UndoMove(moves[m]);

                if (bestEvaluation < evaluation)
                {
                    bestEvaluation = evaluation;
                    bestMove = moves[m];
                }

                alpha = Math.Max(alpha, bestEvaluation);
                if (alpha >= beta) break;
            }

            //After finding best move from this board state,
            //update TT with new best move;
            //also mark if this is a "killer move"
            if (!inQSearch)
            {
                transposition.evaluation = bestEvaluation;
                transposition.zobristHash = m_board.ZobristKey;
                transposition.move = bestMove;
                if (bestEvaluation < startingAlpha)
                    transposition.flag = 3;
                else if (bestEvaluation >= beta)
                {
                    transposition.flag = 2;
                    if (!bestMove.IsCapture)
                        m_killerMoves[depth] = bestMove;
                }
                else transposition.flag = 1;
                transposition.depth = (sbyte)depth;
            }

            return bestEvaluation;
        }

        void OrderMoves(ref Move[] moves, int depth)
        {
            int[] movePriorities = new int[moves.Length];
            for (int m = 0; m < moves.Length; m++) movePriorities[m] = GetMovePriority(moves[m], depth);
            Array.Sort(movePriorities, moves);
            Array.Reverse(moves);
        }

        int GetMovePriority(Move move, int depth)
        {
            int priority = 0;
            //a move in the TT is likely PV
            Transposition tp = m_TPTable[m_board.ZobristKey & 0x7FFFFF];
            if (tp.move == move && tp.zobristHash == m_board.ZobristKey)
                priority += 100000;
            //Captures ordered in MVVLVA order
            else if (move.IsCapture)
                priority = 1000 + 10 * (int)move.CapturePieceType - (int)move.MovePieceType;
            //Check for a killer move
            else if (depth >= 0 && move.Equals(m_killerMoves[depth]))
                priority = 1;
            //if none of the previous conditions passed priority = 0
            return priority;
        }

        int Evaluate()
        {
#if LOGGING
        m_evals++;
#endif

            //Pulled from JW's example bot's implementation of compressed PeSTO (ComPresSTO?)

            int mg = 0, eg = 0, phase = 0;

            foreach (bool stm in new[] { true, false })
            {
                for (var p = PieceType.Pawn; p <= PieceType.King; p++)
                {
                    int piece = (int)p, ind;
                    ulong mask = m_board.GetPieceBitboard(p, stm);
                    while (mask != 0)
                    {
                        phase += piecePhase[piece];
                        ind = 128 * (piece - 1) + BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (stm ? 56 : 0);
                        mg += getPstVal(ind) + k_pieceValues[piece];
                        eg += getPstVal(ind + 64) + k_pieceValues[piece];
                    }
                }

                mg = -mg;
                eg = -eg;
            }

            return (mg * phase + eg * (24 - phase)) / 24 * (m_board.IsWhiteToMove ? 1 : -1);
        }

        int getPstVal(int psq)
        {
            return (int)(((psts[psq / 10] >> (6 * (psq % 10))) & 63) - 20) * 8;
        }

        bool ShouldExecuteNextDepth(Timer timer, int maxThinkTime)
        {
            int currentThinkTime = timer.MillisecondsElapsedThisTurn;
            return ((maxThinkTime - currentThinkTime) > currentThinkTime * 2);
        }



#if LOGGING
private void PrintPV(Board board, int depth)
{
    ulong zHash = board.ZobristKey;
    Transposition tp = m_TPTable[zHash & 0x7FFFFF];
    if(tp.flag != INVALID && tp.zobristHash == zHash && depth >= 0)
    {
        Console.Write("{0} | ", tp.move);
        board.MakeMove(tp.move);
        PrintPV(board, depth - 1);
    }
    Console.WriteLine("");
}
#endif



#if VISUALIZER
    ulong GetBoardControl(PieceType pt, bool forWhite)
    {
        ulong uncontrolledBitboard = 0xffffffffffffffff;
        ulong controlledBitboard = 0;
        PieceList whitePieces = m_board.GetPieceList(pt, true);
        PieceList blackPieces = m_board.GetPieceList(pt, false);
        int whitePieceNum = whitePieces.Count;
        int blackPieceNum = blackPieces.Count;
        int maxPieceNum = Math.Max(whitePieceNum, blackPieceNum);
        for(int j = 0; j < maxPieceNum; j++)
        {
            ulong whitePieceBitboard = whitePieceNum > j ? GetAttacks(whitePieces[j].Square, pt,  true) : 0;
            ulong blackPieceBitboard = blackPieceNum > j ? GetAttacks(blackPieces[j].Square, pt, false) : 0;
            uncontrolledBitboard &= ~(whitePieceBitboard | blackPieceBitboard);
            controlledBitboard |= whitePieceBitboard;
            controlledBitboard &= ~blackPieceBitboard;
        }
        return forWhite ? controlledBitboard : ~(controlledBitboard ^ uncontrolledBitboard);
    }
    ulong GetAttacks(Square square, PieceType pt, bool isWhite)
    {
        return pt switch
        {
            PieceType.Pawn => BitboardHelper.GetPawnAttacks(square, isWhite),
            PieceType.Knight => BitboardHelper.GetKnightAttacks(square),
            PieceType.King => BitboardHelper.GetKingAttacks(square),
            _ => BitboardHelper.GetSliderAttacks(pt, square, m_board),
        };
    }
#endif
    }
}