using Kodnix.Character;

namespace KianaBH.Util;

public class IConsole
{
    public static readonly string PrefixContent = "[KianaBH]> ";
    public static readonly string Prefix = $"\u001b[38;2;255;192;203m{PrefixContent}\u001b[0m";

    private static readonly int HistoryMaxCount = 10;

    /// <summary>
    /// 类似 Danheng：空回车时自动执行的命令
    /// </summary>
    public static string DefaultCommandOnEmptyEnter { get; set; } = "status";

    public static List<char> Input { get; set; } = [];
    private static int CursorIndex { get; set; } = 0;
    private static readonly List<string> InputHistory = [];
    private static int HistoryIndex = -1;

    public static event Action<string>? OnConsoleExcuteCommand;

    public static void InitConsole()
    {
        try
        {
            Console.Title = ConfigManager.Config.GameServer.GameServerName;
        }
        catch
        {
            // ignore
        }
    }

    public static int GetWidth(string str)
        => str.ToCharArray().Sum(EastAsianWidth.GetLength);

    public static void RedrawInput(List<char> input, bool hasPrefix = true)
        => RedrawInput(new string([.. input]), hasPrefix);

    public static void RedrawInput(string input, bool hasPrefix = true)
    {
        try
        {
            var length = GetWidth(input);
            if (hasPrefix)
            {
                input = Prefix + input;
                length += GetWidth(PrefixContent);
            }

            if (Console.GetCursorPosition().Left > 0)
                Console.SetCursorPosition(0, Console.CursorTop);

            Console.Write(input + new string(' ', Math.Max(0, Console.BufferWidth - length)));
            Console.SetCursorPosition(Math.Min(length, Console.BufferWidth - 1), Console.CursorTop);
        }
        catch
        {
            // ignore non-TTY
        }
    }

    #region Handlers（核心：Danheng 逻辑在这里）

    public static void HandleEnter()
    {
        var input = new string([.. Input]).Trim();

        // ✅ Danheng 风格：空输入也执行
        if (string.IsNullOrEmpty(input))
            input = DefaultCommandOnEmptyEnter;

        // 换行（必须）
        Console.WriteLine();

        // 重置状态
        Input = [];
        CursorIndex = 0;
        HistoryIndex = InputHistory.Count;

        // 历史记录：默认命令不进历史
        if (input != DefaultCommandOnEmptyEnter)
        {
            if (InputHistory.Count >= HistoryMaxCount)
                InputHistory.RemoveAt(0);
            InputHistory.Add(input);
        }

        // 处理 /
        if (input.StartsWith('/'))
            input = input[1..].Trim();

        OnConsoleExcuteCommand?.Invoke(input);
    }

    public static void HandleBackspace()
    {
        try
        {
            if (CursorIndex <= 0) return;

            CursorIndex--;
            var targetWidth = GetWidth(Input[CursorIndex].ToString());
            Input.RemoveAt(CursorIndex);

            var (left, top) = Console.GetCursorPosition();
            Console.SetCursorPosition(Math.Max(0, left - targetWidth), top);

            var remain = new string([.. Input.Skip(CursorIndex)]);
            Console.Write(remain + new string(' ', targetWidth));
            Console.SetCursorPosition(Math.Max(0, left - targetWidth), top);
        }
        catch
        {
            // ignore
        }
    }

    public static void HandleUpArrow()
    {
        if (InputHistory.Count == 0) return;

        if (HistoryIndex > 0)
        {
            HistoryIndex--;
            var history = InputHistory[HistoryIndex];
            Input = [.. history];
            CursorIndex = Input.Count;
            RedrawInput(Input);
        }
    }

    public static void HandleDownArrow()
    {
        if (HistoryIndex >= InputHistory.Count) return;

        HistoryIndex++;
        if (HistoryIndex >= InputHistory.Count)
        {
            HistoryIndex = InputHistory.Count;
            Input = [];
            CursorIndex = 0;
        }
        else
        {
            var history = InputHistory[HistoryIndex];
            Input = [.. history];
            CursorIndex = Input.Count;
        }
        RedrawInput(Input);
    }

    public static void HandleLeftArrow()
    {
        try
        {
            if (CursorIndex <= 0) return;

            var (left, top) = Console.GetCursorPosition();
            CursorIndex--;
            Console.SetCursorPosition(
                Math.Max(0, left - GetWidth(Input[CursorIndex].ToString())),
                top
            );
        }
        catch
        {
            // ignore
        }
    }

    public static void HandleRightArrow()
    {
        try
        {
            if (CursorIndex >= Input.Count) return;

            var (left, top) = Console.GetCursorPosition();
            CursorIndex++;
            Console.SetCursorPosition(
                left + GetWidth(Input[CursorIndex - 1].ToString()),
                top
            );
        }
        catch
        {
            // ignore
        }
    }

    public static void HandleInput(ConsoleKeyInfo keyInfo)
    {
        if (char.IsControl(keyInfo.KeyChar)) return;

        if (GetWidth(new string([.. Input])) + GetWidth(keyInfo.KeyChar.ToString())
            >= Console.BufferWidth - PrefixContent.Length)
            return;

        HandleInput(keyInfo.KeyChar);
    }

    public static void HandleInput(char keyChar)
    {
        try
        {
            Input.Insert(CursorIndex, keyChar);
            CursorIndex++;

            var (left, top) = Console.GetCursorPosition();
            Console.Write(new string([.. Input.Skip(CursorIndex - 1)]));
            Console.SetCursorPosition(
                Math.Min(Console.BufferWidth - 1, left + GetWidth(keyChar.ToString())),
                top
            );
        }
        catch
        {
            // ignore
        }
    }

    #endregion

    public static void ListenConsole()
    {
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
}

