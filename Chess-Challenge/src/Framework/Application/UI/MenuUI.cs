using Raylib_cs;
using System.Numerics;
using System;
using System.IO;
using ChessChallenge.Enemy;

namespace ChessChallenge.Application
{
    public static class MenuUI
    {
        public static void DrawButtons(ChallengeController controller)
        {
            Vector2 buttonPos = UIHelper.Scale(new Vector2(260, 120));
            Vector2 buttonSize = UIHelper.Scale(new Vector2(260, 55));
            float spacing = buttonSize.Y * 1.2f;
            float breakSpacing = spacing * 0.6f;

            // Game Buttons
            if (NextButtonInRow("Human vs MyBot", ref buttonPos, spacing, buttonSize))
            {
                var whiteType = controller.HumanWasWhiteLastGame ? ChallengeController.PlayerType.MyBot : ChallengeController.PlayerType.Human;
                var blackType = !controller.HumanWasWhiteLastGame ? ChallengeController.PlayerType.MyBot : ChallengeController.PlayerType.Human;
                controller.StartNewGame(whiteType, blackType);
            }
            if (NextButtonInRow("MyBot vs MyBot", ref buttonPos, spacing, buttonSize))
            {
                controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.MyBot);
            }
            if (NextButtonInRow("MyBot vs UserBot", ref buttonPos, spacing, buttonSize))
            {
                controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.UserBot);
            }
            if (NextButtonInRow("MyBot vs Stockfish", ref buttonPos, spacing, buttonSize))
            {
                controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.Stockfish);
            }
            if (NextButtonInRow("UserBot vs Stockfish", ref buttonPos, spacing, buttonSize))
            {
                controller.StartNewBotMatch(ChallengeController.PlayerType.UserBot, ChallengeController.PlayerType.Stockfish);
            }
            NextTextInRow($"Stockfish ELO = {StockfishBot.ELO})", ref buttonPos, buttonSize, 1, UIHelper.ScaleInt(32));
            (bool minusTen, bool plusTen) = NextSplitButtonInRow("-10", "+10", ref buttonPos, spacing, buttonSize);
            if (minusTen) StockfishBot.ELO -= 10;
            else if (plusTen) StockfishBot.ELO += 10;
            (bool minusHundred, bool plusHundred) = NextSplitButtonInRow("-100", "+100", ref buttonPos, spacing, buttonSize);
            if (minusHundred) StockfishBot.ELO -= 100;
            else if (plusHundred) StockfishBot.ELO += 100;
            // Page buttons
            buttonPos.Y += breakSpacing;

            if (NextButtonInRow("Save Games", ref buttonPos, spacing, buttonSize))
            {
                string pgns = controller.AllPGNs;
                string directoryPath = Path.Combine(FileHelper.AppDataPath, "Games");
                Directory.CreateDirectory(directoryPath);
                string fileName = FileHelper.GetUniqueFileName(directoryPath, "games", ".txt");
                string fullPath = Path.Combine(directoryPath, fileName);
                File.WriteAllText(fullPath, pgns);
                ConsoleHelper.Log("Saved games to " + fullPath, false, ConsoleColor.Blue);
            }

            // Window and quit buttons
            buttonPos.Y += breakSpacing;

            bool isBigWindow = Raylib.GetScreenWidth() > Settings.ScreenSizeSmall.X;
            string windowButtonName = isBigWindow ? "Smaller Window" : "Bigger Window";
            if (NextButtonInRow(windowButtonName, ref buttonPos, spacing, buttonSize))
            {
                Program.SetWindowSize(isBigWindow ? Settings.ScreenSizeSmall : Settings.ScreenSizeBig);
            }
            if (NextButtonInRow("Exit (ESC)", ref buttonPos, spacing, buttonSize))
            {
                Environment.Exit(0);
            }

            bool NextButtonInRow(string name, ref Vector2 pos, float spacingY, Vector2 size)
            {
                bool pressed = UIHelper.Button(name, pos, size);
                pos.Y += spacingY;
                return pressed;
            }

            void NextTextInRow(string name, ref Vector2 pos, Vector2 bounds, int fontSpacing, int size)
            {
                UIHelper.DrawText(name, pos, size, fontSpacing, Color.WHITE, UIHelper.AlignH.Centre);
                pos.Y += (float)size * 1.5f;
            }

            (bool, bool) NextSplitButtonInRow(string left, string right, ref Vector2 pos, float spacingY, Vector2 size)
            {
                Vector2 split = new Vector2(size.X / 2, size.Y);
                Vector2 splitPos = new Vector2(pos.X - split.X / 2, pos.Y);
                bool leftPressed = UIHelper.Button(left, splitPos, split);
                splitPos = new Vector2(pos.X + split.X / 2, pos.Y);
                bool rightPressed = UIHelper.Button(right, splitPos, split);
                pos.Y += spacingY;
                return (leftPressed, rightPressed);
            }
        }
    }
}