using WebTools.NET.Models;

namespace WebTools.NET.Demo;

/// <summary>
/// All console formatting for the demo in one place: colors, alignment
/// and rendering of the library result records.
/// </summary>
internal static class ConsoleOutput
{
    private const string Rule = "=====================================================================";

    private const string ThinRule = "---------------------------------------------------------------------";

    private const string OkTag = "[ OK ]";

    private const string FailTag = "[FAIL]";

    private const int TitleMaxLength = 70;

    private const int SnippetMaxLength = 90;

    private const int PreviewMaxLength = 100;

    public static void Banner(string title, string subtitle)
    {
        Console.WriteLine(Rule);
        Console.WriteLine($"  {title}");
        Console.WriteLine($"  {subtitle}");
        Console.WriteLine(Rule);
        Console.WriteLine();
    }

    public static void SectionHeader(int step, string title) =>
        Console.WriteLine($"--- {step}. {title}");

    public static void SummaryTitle()
    {
        Console.WriteLine(Rule);
        Console.WriteLine("  Summary");
        Console.WriteLine(Rule);
    }

    public static void SummaryDivider() => Console.WriteLine(ThinRule);

    public static void RuleLine() => Console.WriteLine(Rule);

    public static void SummaryTotal(int passed, int total, double totalSeconds) =>
        Console.WriteLine($"  {passed}/{total} sections completed successfully - total {totalSeconds:F1} s");

    public static void BlankLine() => Console.WriteLine();

    public static void Description(string text)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    {text}");
        Console.ResetColor();
    }

    public static void Info(string label, string value)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write($"    {label,-19}: ");
        Console.ResetColor();
        Console.WriteLine(value);
    }

    public static void Ok(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"    {OkTag} {message}");
        Console.ResetColor();
    }

    public static void Fail(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"    {FailTag} {message}");
        Console.ResetColor();
    }

    public static void Detail(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"           {message}");
        Console.ResetColor();
    }

    public static void Note(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"    [note] {message}");
        Console.ResetColor();
    }

    public static void Elapsed(TimeSpan elapsed)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    Elapsed: {elapsed.TotalSeconds:F1} s");
        Console.ResetColor();
    }

    public static void SummaryLine(string name, bool ok, string detail, TimeSpan elapsed)
    {
        Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
        Console.Write($"  {(ok ? OkTag : FailTag)} ");
        Console.ResetColor();
        Console.Write($"{name,-62} {elapsed.TotalSeconds,6:F1} s");

        if (!ok && !string.IsNullOrEmpty(detail))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"   {detail}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    public static void PrintSearchResult(SearchResult result)
    {
        if (!result.Success)
        {
            Fail($"search failed: {result.ErrorMessage}");
            return;
        }

        if (result.Results.Count == 0)
        {
            Note("search returned no results (the provider was likely blocked by bot detection)");
            return;
        }

        Ok($"{result.Results.Count} result(s):");
        for (var i = 0; i < result.Results.Count; i++)
        {
            var item = result.Results[i];
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"      {i + 1}. {Truncate(item.Title, TitleMaxLength)}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"         {item.Url}");
            Console.ResetColor();
            if (!string.IsNullOrWhiteSpace(item.Snippet))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"         {Truncate(item.Snippet, SnippetMaxLength)}");
                Console.ResetColor();
            }
        }
    }

    public static string Preview(string text) => Truncate(text, PreviewMaxLength);

    private static string Truncate(string value, int max)
    {
        var oneLine = string.Join(" ", value.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries));
        return oneLine.Length <= max ? oneLine : oneLine[..max] + "...";
    }
}
