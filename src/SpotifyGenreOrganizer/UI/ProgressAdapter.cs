using Spectre.Console;
using SpotifyTools.Sync;

namespace SpotifyGenreOrganizer.UI;

/// <summary>
/// Adapter that bridges event-driven SyncProgressEventArgs to Spectre.Console Progress context
/// </summary>
public class ProgressAdapter : IDisposable
{
    private readonly ISyncService _syncService;
    private ProgressTask? _currentTask;
    private ProgressContext? _context;

    public ProgressAdapter(ISyncService syncService)
    {
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _syncService.ProgressChanged += OnProgressChanged;
    }

    /// <summary>
    /// Runs an async action with a Spectre progress display
    /// </summary>
    public async Task RunWithProgressAsync(Func<Task> action, string description)
    {
        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                _context = ctx;
                _currentTask = ctx.AddTask(description);

                try
                {
                    await action();

                    if (_currentTask != null && !_currentTask.IsFinished)
                    {
                        _currentTask.Value = _currentTask.MaxValue;
                        _currentTask.StopTask();
                    }
                }
                finally
                {
                    _context = null;
                    _currentTask = null;
                }
            });
    }

    /// <summary>
    /// Runs an async function with a Spectre progress display and returns the result
    /// </summary>
    public async Task<T> RunWithProgressAsync<T>(Func<Task<T>> func, string description)
    {
        T result = default!;

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                _context = ctx;
                _currentTask = ctx.AddTask(description);

                try
                {
                    result = await func();

                    if (_currentTask != null && !_currentTask.IsFinished)
                    {
                        _currentTask.Value = _currentTask.MaxValue;
                        _currentTask.StopTask();
                    }
                }
                finally
                {
                    _context = null;
                    _currentTask = null;
                }
            });

        return result;
    }

    private void OnProgressChanged(object? sender, SyncProgressEventArgs e)
    {
        if (_currentTask == null || _context == null) return;

        // Update task description with stage and message
        _currentTask.Description = $"[yellow]{e.Stage}[/]: {e.Message}";

        // Update progress
        if (e.Total > 0)
        {
            _currentTask.MaxValue = e.Total;
            _currentTask.Value = e.Current;
        }
        else
        {
            // Indeterminate progress (no total known)
            _currentTask.IsIndeterminate = true;
        }
    }

    public void Dispose()
    {
        _syncService.ProgressChanged -= OnProgressChanged;
    }
}
