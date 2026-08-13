using System.Diagnostics;

namespace WebTools.NET.Demo;

/// <summary>
/// Runs the demo sections sequentially. Each section is isolated - a failure
/// is reported but does not abort the run - and the collected outcomes are
/// rendered as a summary table at the end.
/// </summary>
internal sealed class DemoRunner
{
    private readonly List<SectionResult> _results = [];

    private int _step;

    public bool AllSucceeded => _results.Count > 0 && _results.All(r => r.Ok);

    public async Task RunSectionAsync(string title, string description, Func<Task> body)
    {
        _step++;
        ConsoleOutput.SectionHeader(_step, title);
        ConsoleOutput.Description(description);

        var watch = Stopwatch.StartNew();
        var ok = true;
        var error = string.Empty;
        try
        {
            await body();
        }
        catch (Exception ex)
        {
            ok = false;
            error = ex.Message.Split('\n')[0].Trim();
            ConsoleOutput.Fail($"{ex.GetType().Name}: {error}");
        }

        watch.Stop();
        ConsoleOutput.Elapsed(watch.Elapsed);
        ConsoleOutput.BlankLine();
        _results.Add(new SectionResult($"{_step}. {title}", ok, error, watch.Elapsed));
    }

    public void PrintSummary()
    {
        ConsoleOutput.SummaryTitle();

        foreach (var result in _results)
        {
            ConsoleOutput.SummaryLine(result.Name, result.Ok, result.Detail, result.Elapsed);
        }

        ConsoleOutput.SummaryDivider();
        var passed = _results.Count(r => r.Ok);
        var totalSeconds = _results.Sum(r => r.Elapsed.TotalSeconds);
        ConsoleOutput.SummaryTotal(passed, _results.Count, totalSeconds);
        ConsoleOutput.RuleLine();
    }
}
