using Kodnix.Character;

namespace EggLink.DanhengServer.Util
{
    public static class IConsole
    {
        public const string PrefixContent = "> ";
        private const string PinkColor = "\e[38;2;255;192;203m";
        private const string RedColor = "\e[38;2;255;0;0m";
        private const string ResetColor = "\e[0m";

        /// <summary>
        /// 当输入为空时，按 Enter 自动执行的命令
        /// </summary>
        public static string DefaultCommandOnEmptyEnter { get; set; } = "status";

        // coloured prefix
        public static string Prefix => $"{(IsCommandValid ? ResetColor : RedColor)}{PrefixContent}{ResetColor}";

        public static bool IsCommandValid { get; private set; } = true;
        private const int HistoryMaxCount = 10;

        public static readonly object ConsoleLock = new();

        public static List<char> Input { get; set; } = [];
        private static int CursorIndex { get; set; }
        private static readonly List<string> InputHistory = [];
        private static int HistoryIndex = -1;

        public static event Action<string>? OnConsoleExcuteCommand;

        public static bool ForceDisable { get; set; } = false;

        private static bool? _isConsoleAvailable;
        public static bool IsConsoleAvailable
        {
            get
            {
                if (ForceDisable) return false;
                if (_isConsoleAvailable.HasValue) return _isConsoleAvailable.Value;

                try
                {
                    var term = Environment.GetEnvironmentVariable("TERM");
                    if (string.IsNullOrEmpty(term) || term == "dumb")
                    {
                        _isConsoleAvailable = false;
                        return false;
                    }

                    _ = Environment.UserInteractive;
                    if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
                    {
                        _isConsoleAvailable = false;
                        return false;
                    }

                    if (Console.WindowWidth <= 0 || Console.BufferWidth <= 0)
                    {
                        _isConsoleAvailable = false;
                        return false;
                    }

                    _isConsoleAvailable = true;
                    return true;
                }
                catch
                {
                    _isConsoleAvailable = false;
                    return false;
                }
            }
        }

        public static void InitConsole()
        {
            if (!IsConsoleAvailable) return;
            try { Console.Title = "Danheng Server"; } catch { }
        }

        public static int GetWidth(string str)
            => str.ToCharArray().Sum(EastAsianWidth.GetLength);

        public static void RedrawInput(List<char> input, bool hasPrefix = true)
            => RedrawInput(new string([.. input]), hasPrefix);

        public static void RedrawInput(string input, bool hasPrefix = true)
        {
            if (!IsConsoleAvailable) return;

            lock (ConsoleLock)
            {
                UpdateCommandValidity(input);

                var inputStr = input;
                if (hasPrefix)
                {
                    inputStr = Prefix + input;
                }

                var totalWidth = GetWidth(inputStr);
                var cursorEffectiveIndex = CursorIndex + (hasPrefix ? GetWidth(PrefixContent) : 0);

                Console.Write('\r');
                Console.Write(inputStr);

                int clearLen = Console.BufferWidth - totalWidth;
                if (clearLen < 0) clearLen = 0;
                if (clearLen > 0)
                {
                    Console.Write(new string(' ', clearLen));
                }

                Console.Write('\r');

                int moveRight = cursorEffectiveIndex;
                if (moveRight >= Console.BufferWidth)
                    moveRight = Console.BufferWidth - 1;
                if (moveRight > 0)
                {
                    Console.Write($"\x1b[{moveRight}C");
                }
            }
        }

        private static void UpdateCommandValidity(string input)
        {
            IsCommandValid = CheckCommandValid(input);
        }

        #region Handlers

        public static void HandleEnter()
        {
            lock (ConsoleLock)
            {
                var input = new string([.. Input]).Trim();

                // ✅ 自动回车核心：空输入也执行
                if (string.IsNullOrEmpty(input))
                    input = DefaultCommandOnEmptyEnter;

                // 换行
                Console.WriteLine();

                // 重置状态
                Input = [];
                CursorIndex = 0;
                HistoryIndex = InputHistory.Count;

                // 历史记录（默认命令不进历史）
                if (input != DefaultCommandOnEmptyEnter)
                {
                    if (InputHistory.Count >= HistoryMaxCount)
                        InputHistory.RemoveAt(0);
                    InputHistory.Add(input);
                }

                // 处理 /
                if (input.StartsWith('/'))
                    input = input[1..].Trim();

                IsCommandValid = true;

                // 执行
                OnConsoleExcuteCommand?.Invoke(input);
            }
        }

        public static void HandleBackspace()
        {
            lock (ConsoleLock)
            {
                if (CursorIndex <= 0) return;
                if (CursorIndex > Input.Count) CursorIndex = Input.Count;

                CursorIndex--;
                Input.RemoveAt(CursorIndex);
                RedrawInput(Input);
            }
        }

        public static void HandleUpArrow()
        {
            lock (ConsoleLock)
            {
                if (InputHistory.Count == 0) return;
                if (HistoryIndex <= 0) return;

                HistoryIndex--;
                var history = InputHistory[HistoryIndex];
                Input = [.. history];
                CursorIndex = Input.Count;

                UpdateCommandValidity(history);
                RedrawInput(Input);
            }
        }

        public static void HandleDownArrow()
        {
            lock (ConsoleLock)
            {
                if (HistoryIndex >= InputHistory.Count) return;

                HistoryIndex++;
                if (HistoryIndex >= InputHistory.Count)
                {
                    HistoryIndex = InputHistory.Count;
                    Input = [];
                    CursorIndex = 0;
                    IsCommandValid = true;
                }
                else
                {
                    var history = InputHistory[HistoryIndex];
                    Input = [.. history];
                    CursorIndex = Input.Count;
                    UpdateCommandValidity(history);
                }
                RedrawInput(Input);
            }
        }

        public static void HandleLeftArrow()
        {
            lock (ConsoleLock)
            {
                if (CursorIndex <= 0) return;
                CursorIndex--;
                RedrawInput(Input);
            }
        }

        public static void HandleRightArrow()
        {
            lock (ConsoleLock)
            {
                if (CursorIndex >= Input.Count) return;
                CursorIndex++;
                RedrawInput(Input);
            }
        }

        public static void HandleInput(ConsoleKeyInfo keyInfo)
        {
            lock (ConsoleLock)
            {
                if (char.IsControl(keyInfo.KeyChar)) return;

                var newWidth = GetWidth(new string([.. Input])) +
                               GetWidth(keyInfo.KeyChar.ToString());

                if (newWidth >= (Console.BufferWidth - GetWidth(PrefixContent)))
                    return;

                HandleInput(keyInfo.KeyChar);
            }
        }

        public static void HandleInput(char keyChar)
        {
            lock (ConsoleLock)
            {
                if (CursorIndex < 0) CursorIndex = 0;
                if (CursorIndex > Input.Count) CursorIndex = Input.Count;

                Input.Insert(CursorIndex, keyChar);
                CursorIndex++;
                RedrawInput(Input);
            }
        }

        #endregion

        public static void ListenConsole()
        {
            if (!IsConsoleAvailable) return;

            while (true)
            {
                try
                {
                    var keyInfo = Console.ReadKey(true);

                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.Enter:
                            HandleEnter();
                            break;
                        case ConsoleKey.Backspace:
                            HandleBackspace();
                            break;
                        case ConsoleKey.LeftArrow:
                            HandleLeftArrow();
                            break;
                        case ConsoleKey.RightArrow:
                            HandleRightArrow();
                            break;
                        case ConsoleKey.UpArrow:
                            HandleUpArrow();
                            break;
                        case ConsoleKey.DownArrow:
                            HandleDownArrow();
                            break;
                        default:
                            HandleInput(keyInfo);
                            break;
                    }
                }
                catch (InvalidOperationException)
                {
                    Thread.Sleep(50);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private static bool CheckCommandValid(string input)
        {
            if (string.IsNullOrEmpty(input))
                return true;

            var invalidChars = new[] { '@', '#', '$', '%', '&', '*' };
            return !invalidChars.Any(c => input.Contains(c));
        }
    }
}

