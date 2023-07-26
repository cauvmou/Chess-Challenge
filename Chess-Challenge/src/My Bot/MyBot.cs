using System;
using System.Collections.Generic;
using ChessChallenge.API;

public class Param {
    public IList<float> weights { get; set; }
    public IList<float> bias { get; set; }
    public IList<int> dims { get; set; }

    public override string ToString() {
        string s = "";
        s += "weights: ";
        foreach (float f in weights) {
            s += f.ToString() + ", ";
        }
        s += "\n";
        s += "bias: ";
        foreach (float f in bias) {
            s += f.ToString() + ", ";
        }
        s += "\n";
        s += "dims: ";
        foreach (ulong u in dims) {
            s += u.ToString() + ", ";
        }
        s += "\n";
        return s;
    }
}


public class MyBot : IChessBot
{
    private int[] PieceTypeToValue = new int[] {0, 100, 300, 300, 500, 900, 10000}; // None, Pawn, Knight, Bishop, Rook, Queen, King

    private static String thingy = """aklsdjfklji43p9jfiuajsdklfjaklsdfiu43h8qjaiodfjlköasjgklajoifpa3ji9fjuaiofjlaksjfioapweji9ajsdiofjaklsjfioaweasdfjaksldfjklöasdjföklasjdklföjasklfjlkadsöjfklaösjfklöasjdköfljasköldfjakölsjföklasdjklöasdjföklasjdklföjasklfjlkadsöjfklaösjfklöasjdköfljasköldfjakölsjföklasd""";

    private static String jsonParams = System.IO.File.ReadAllText("src/My Bot/params.json");
    
    // deserialize json_params into Param list
    private IList<Param>? wandb = System.Text.Json.JsonSerializer.Deserialize<IList<Param>>(jsonParams);
    public Move Think(Board board, Timer timer)
    {
        /*foreach (float val in input)
        {
            Console.Write(val + ",");
        }*/

        // Console.WriteLine("out: " + output[0]);
        Console.WriteLine(BoardValue(board));
        Console.WriteLine("Material score: " + MaterialScore(board));
        // Console.WriteLine(res.ToString());
        // Console.WriteLine(json_params);

        var depth = 4;

        var moves = board.GetLegalMoves();
        var move = moves[new Random().Next(moves.Length)];
        // System.Console.WriteLine("Fallback: " + move);
        var chosen = Minimax(board, move, BoardvalueWithMove(board, move), depth, board.IsWhiteToMove);
        // System.Console.WriteLine("Chose:    " + chosen.Item1);


        return chosen.Item1;
    }

        private (Move, double) Minimax(Board board, Move lastMove, double lastValue, int depth, bool isMax) {
        // Depth check
        if (depth == 0) return (lastMove, BoardValue(board));

        Move[] legalMoves = board.GetLegalMoves();
        (Move, double) bestMove = (lastMove, isMax ? double.MinValue : double.MaxValue);

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

        
    private void ConvertChar(char c, ref IList<float> neuralInput) {
        if (c >= '0' && c <= '8') {
            for (int i = 0; i < c - '0'; i++) {
                neuralInput.Add(0.0f);
            }
        }
        switch (c) {
            case 'r': neuralInput.Add(-0.1f); break;
            case 'b': neuralInput.Add(-0.2f); break;
            case 'n': neuralInput.Add(-0.3f); break;
            case 'q': neuralInput.Add(-0.4f); break;
            case 'k': neuralInput.Add(-0.5f); break;
            case 'p': neuralInput.Add(-0.6f); break;
            case 'R': neuralInput.Add(0.1f); break;
            case 'B': neuralInput.Add(0.2f); break;
            case 'N': neuralInput.Add(0.3f); break;
            case 'Q': neuralInput.Add(0.4f); break;
            case 'K': neuralInput.Add(0.5f); break;
            case 'P': neuralInput.Add(0.6f); break;
        }
        
    }

    private IList<float> ToNeuralInput(String fen) {
        String fenBoard = fen.Split(' ')[0];
        IList<float> neuralInput = new List<float>();
        foreach(String row in fenBoard.Split('/')) {
            foreach(char c in row) {
                ConvertChar(c, ref neuralInput);
            }
        }
        return neuralInput;
    }

    /// <summary> Postive = White is better / Negative = Black is better </summary>
    private float BoardValue(Board board) 
    {
        // Console.WriteLine(neuralInput[0].ToString());

        IList<float> input = ToNeuralInput(board.GetFenString());

        float[] output = new float[0];
        int currentLayer = 0;
        foreach(Param param in wandb) {
            output = new float[param.dims[1]];
            for(int col=0; col<param.dims[1]; col++) {
                float sum = 0;
                for(int row=0; row<param.dims[0]; row++) {
                    sum += input[row] * param.weights[row * param.dims[1] + col];
                }
                float val = sum + param.bias[col];
                // next may be tanh()
                if (val > 0 || currentLayer == wandb.Count-1) {
                    output[col] = val;
                } else {
                    output[col] = 0.0f;
                }
                
            }
            input = output;
            currentLayer+=1;
        }
        return output[0];
    }

    private int MaterialScore(Board board) {
        int materialScore = 0;
        foreach (var list in board.GetAllPieceLists()) 
        {
            foreach (var piece in list) 
            {
                materialScore += (piece.IsWhite ? 1 : -1) * PieceTypeToValue[(int)piece.PieceType];
            }
        }
        return materialScore;
    }

        private double BoardvalueWithMove(Board board, Move move) {
            board.MakeMove(move);
            var s = BoardValue(board);
            board.UndoMove(move);
            return s;
    }
}