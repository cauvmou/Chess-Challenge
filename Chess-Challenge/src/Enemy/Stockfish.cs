﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ChessChallenge.API;

namespace ChessChallenge.Enemy
{
    /// <summary> Stockfish </summary>
    public class StockfishBot : IChessBot
    {

        private Stockfish stockfish;

        private static int Elo = 1320;

        public static int ELO
        {
            get => Elo;
            set
            {
                if (value >= 1320 && value <= 3190)
                {
                    Elo = value;
                }
            }
        }

        public StockfishBot()
        {
            System.Console.WriteLine("OS detected:   " + (IsLinux ? "Linux" : "Windows"));
            System.Console.WriteLine("AVX supported: " + (HasAvxSupport ? "Yes" : "No"));
            string path = (HasAvxSupport, IsLinux) switch
            {
                (true, true) => GetResourcePath("Stockfish", "avx2", "linux", "stockfish-ubuntu-x86-64-avx2"),
                (false, true) => GetResourcePath("Stockfish", "popcnt", "linux", "stockfish-ubuntu-x86-64-modern"),
                (true, false) => GetResourcePath("Stockfish", "avx2", "windows", "stockfish-windows-x86-64-avx2"),
                (false, false) => GetResourcePath("Stockfish", "popcnt", "windows", "stockfish-windows-x86-64-modern"),
            };
            stockfish = new Stockfish(path, depth: 4, settings: new Stockfish.Settings
            {
                Threads = 8,
                SlowMover = 10,
                //SkillLevel = 8,
                Elo = Elo,
                MoveOverhead = 0,
                MultiPV = 1,
            });
            stockfish.StartNewGame();
            System.Console.WriteLine($"Stockfish started with UCI-Elo {Elo}");
        }

        public static string GetResourcePath(params string[] localPath)
        {
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (path == null) throw new FileNotFoundException();
            return Path.Combine(path, "resources", Path.Combine(localPath));
        }

        public static bool IsLinux
        {
            get
            {
                int p = (int)Environment.OSVersion.Platform;
                return (p == 4) || (p == 6) || (p == 128);
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern long GetEnabledXStateFeatures();

        public static bool HasAvxSupport
        {
            get
            {
                try
                {
                    return (GetEnabledXStateFeatures() & 4) != 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        public Move Think(Board board, Timer timer)
        {
            (int, int) time = board.IsWhiteToMove ? (timer.MillisecondsRemaining, timer.OpponentMillisecondsRemaining) : (timer.OpponentMillisecondsRemaining, timer.MillisecondsRemaining);
            stockfish.SetFenPosition(board.GetFenString());
            string? move = stockfish.GetBestMoveTime(time.Item1, time.Item2, 300);
            return move == null ? Move.NullMove : new Move(move, board);
        }

        private class Stockfish
        {

            private StockfishProcess process;
            private Settings settings;
            private int depth;
            private const int MAX_TRIES = 200;

            public Stockfish(string path, int depth = 4, Settings? settings = null)
            {
                this.settings = settings == null ? new Settings() : settings;
                this.process = new StockfishProcess(path);
                this.depth = depth;
                foreach (var property in this.settings.GetPropertiesAsDictionary())
                {
                    SetOption(property.Key, property.Value);
                }
                StartNewGame();
            }

            private bool IsReady()
            {
                Send("isready");
                var tries = 0;
                while (tries < MAX_TRIES)
                {
                    ++tries;

                    if (process.ReadLine() == "readyok")
                    {
                        return true;
                    }
                }
                throw new ApplicationException("Max tries exceeded!");
            }

            private void SetOption(string name, string value)
            {
                Send($"setoption name {name} value {value}");
            }

            public void StartNewGame()
            {
                Send("ucinewgame");
                if (!IsReady())
                {
                    throw new ApplicationException();
                }
            }

            public void SetFenPosition(string fenPosition)
            {
                Send($"position fen {fenPosition}");
            }

            public string? GetBestMoveTime(int wtime, int btime, int estimate)
            {
                GoTime(wtime, btime, estimate);
                var tries = 0;
                while (true)
                {
                    if (tries > MAX_TRIES)
                    {
                        throw new ApplicationException("Max tries exceeded!");
                    }

                    var data = ReadLineAsList();
                    if (data[0] == "bestmove")
                    {
                        if (data[1] == "(none)")
                        {
                            return null;
                        }

                        return data[1];
                    }
                }
            }

            private void GoTime(int wtime, int btime, int estimate)
            {
                Send($"go wtime {wtime} btime {btime} winc 0 binc 0", estimatedTime: estimate + 100);
            }

            private void Send(string command, int estimatedTime = 100)
            {
                process.WriteLine(command);
                process.Wait(estimatedTime);
            }

            private List<string> ReadLineAsList()
            {
                var data = process.ReadLine();
                return data.Split(' ').ToList();
            }

            public class Settings
            {
                public int Contempt { get; set; }
                public int Threads { get; set; }
                public bool Ponder { get; set; }
                public int MultiPV { get; set; }
                public int Elo { get; set; }
                public int SkillLevel { get; set; }
                public int MoveOverhead { get; set; }
                public int SlowMover { get; set; }
                public bool UCIChess960 { get; set; }

                public Settings(
                    int contempt = 0,
                    int threads = 0,
                    bool ponder = false,
                    int multiPV = 1,
                    int elo = 0,
                    int skillLevel = 8,
                    int moveOverhead = 30,
                    int slowMover = 80,
                    bool uciChess960 = false
                )
                {
                    Contempt = contempt;
                    Ponder = ponder;
                    Threads = threads;
                    MultiPV = multiPV;
                    Elo = elo;
                    SkillLevel = skillLevel;
                    MoveOverhead = moveOverhead;
                    SlowMover = slowMover;
                    UCIChess960 = uciChess960;
                }

                public Dictionary<string, string> GetPropertiesAsDictionary()
                {
                    return new Dictionary<string, string>
                    {
                        ["Contempt"] = Contempt.ToString(),
                        ["Threads"] = Threads.ToString(),
                        ["Ponder"] = Ponder.ToString(),
                        ["MultiPV"] = MultiPV.ToString(),
                        ["UCI_LimitStrength"] = Elo == 0 ? "false" : "true",
                        ["UCI_Elo"] = Elo.ToString(),
                        ["Skill level"] = SkillLevel.ToString(),
                        ["Move Overhead"] = MoveOverhead.ToString(),
                        ["Slow Mover"] = SlowMover.ToString(),
                        ["UCI_Chess960"] = UCIChess960.ToString(),
                    };
                }
            }

            private class StockfishProcess
            {

                private ProcessStartInfo processStartInfo { get; set; }
                private Process process { get; set; }
                private bool debug;

                public StockfishProcess(string path, bool debug = false)
                {
                    processStartInfo = new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true
                    };
                    process = new Process
                    {
                        StartInfo = processStartInfo
                    };
                    this.debug = debug;
                    System.Console.WriteLine("Starting process...");
                    process.Start();
                    System.Console.WriteLine("Process started!");
                    System.Console.WriteLine(ReadLine());
                }

                public void Wait(int millisecond)
                {
                    this.process.WaitForExit(millisecond);
                }

                public void WriteLine(string command)
                {
                    if (this.debug) System.Console.WriteLine("$> " + command);
                    if (process.StandardInput == null)
                    {
                        throw new NullReferenceException();
                    }
                    process.StandardInput.WriteLine(command);
                    process.StandardInput.Flush();
                }

                public string? ReadLine()
                {
                    if (process.StandardOutput == null)
                    {
                        throw new NullReferenceException();
                    }
                    var line = process.StandardOutput.ReadLine();
                    if (this.debug) System.Console.WriteLine("$: " + line);
                    return line;
                }

                public void Start()
                {
                    process.Start();
                }

                ~StockfishProcess()
                {
                    process.Close();
                }
            }
        }
    }
}