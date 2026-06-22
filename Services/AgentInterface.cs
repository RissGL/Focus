using System.IO;
using System.Text.Json;
using WpfApp1.Models;

namespace WpfApp1.Services;

/// <summary>
/// File-based agent interface. CC/agent writes JSON to the inbox file,
/// the app detects it via FileSystemWatcher + polling fallback, imports tasks,
/// and writes a response.
/// </summary>
public class AgentInterface : IDisposable
{
    private readonly string _inboxPath;
    private readonly string _outboxPath;
    private readonly string _dataFolder;
    private FileSystemWatcher? _watcher;
    private readonly Action<List<TodoItem>> _onTasksReceived;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private bool _processing;
    private long _lastProcessedTicks;
    private readonly System.Timers.Timer _pollTimer;

    public string InboxPath => _inboxPath;
    public string OutboxPath => _outboxPath;

    public AgentInterface(string dataFolder, Action<List<TodoItem>> onTasksReceived)
    {
        _dataFolder = dataFolder;
        _inboxPath = Path.Combine(dataFolder, "agent_inbox.json");
        _outboxPath = Path.Combine(dataFolder, "agent_outbox.json");
        _onTasksReceived = onTasksReceived;

        // FileSystemWatcher for immediate detection
        _watcher = new FileSystemWatcher(dataFolder, "agent_inbox.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnInboxChanged;
        _watcher.Created += OnInboxChanged;
        _watcher.Renamed += OnInboxChanged;

        // Polling fallback every 10s in case FSW misses an event
        _pollTimer = new System.Timers.Timer(10_000);
        _pollTimer.Elapsed += (_, _) => _ = ProcessInboxAsync();
        _pollTimer.AutoReset = true;
        _pollTimer.Start();
    }

    private void OnInboxChanged(object sender, FileSystemEventArgs e)
    {
        _ = ProcessInboxAsync();
    }

    private async Task ProcessInboxAsync()
    {
        // Prevent concurrent processing and debounce rapid events
        if (_processing) return;

        // Skip if file hasn't been modified since last check
        try
        {
            var fi = new FileInfo(_inboxPath);
            if (!fi.Exists) return;
            if (fi.Length == 0) return;
            if (fi.LastWriteTime.Ticks <= _lastProcessedTicks) return;
        }
        catch { return; }

        _processing = true;
        try
        {
            await Task.Delay(200); // Let the writer finish

            string json;
            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    json = await File.ReadAllTextAsync(_inboxPath);
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(200);
                }
            }
            // Check we still have content after retries
            json = await File.ReadAllTextAsync(_inboxPath);
            if (string.IsNullOrWhiteSpace(json)) { _processing = false; return; }

            var inbox = JsonSerializer.Deserialize<AgentInbox>(json, _jsonOptions);
            if (inbox?.Tasks is not { Count: > 0 }) { _processing = false; return; }

            var todoItems = new List<TodoItem>();
            var today = DateTime.Today.ToString("yyyy-MM-dd");

            foreach (var t in inbox.Tasks)
            {
                if (string.IsNullOrWhiteSpace(t.Text)) continue;

                var todoType = t.Type?.ToLower() switch
                {
                    "daily" => TodoType.Daily,
                    "long" => TodoType.LongTerm,
                    "longterm" => TodoType.LongTerm,
                    _ => TodoType.ShortTerm
                };

                var boosts = new List<AbilityBoost>();
                if (t.Points is { Count: > 0 })
                {
                    foreach (var kv in t.Points)
                    {
                        boosts.Add(new AbilityBoost
                        {
                            AbilityName = kv.Key,
                            Points = kv.Value,
                            Icon = ""
                        });
                    }
                }

                todoItems.Add(new TodoItem
                {
                    Text = t.Text,
                    Type = todoType,
                    LastResetDate = today,
                    Boosts = boosts
                });
            }

            // Record processed timestamp before clearing
            _lastProcessedTicks = new FileInfo(_inboxPath).LastWriteTime.Ticks;

            // Clear inbox
            await File.WriteAllTextAsync(_inboxPath, "");

            // Write success response
            var outbox = new AgentOutbox
            {
                Status = "ok",
                Message = $"Imported {todoItems.Count} task(s)",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            await File.WriteAllTextAsync(_outboxPath,
                JsonSerializer.Serialize(outbox, _jsonOptions));

            // Notify app to add tasks
            _onTasksReceived(todoItems);
        }
        catch (Exception ex)
        {
            try
            {
                var errorResponse = new AgentOutbox
                {
                    Status = "error",
                    Message = ex.Message,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                await File.WriteAllTextAsync(_outboxPath,
                    JsonSerializer.Serialize(errorResponse, _jsonOptions));
            }
            catch { }
        }
        finally
        {
            _processing = false;
        }
    }

    public void Dispose()
    {
        _pollTimer?.Stop();
        _pollTimer?.Dispose();

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private class AgentInbox
    {
        public List<AgentTask> Tasks { get; set; } = new();
    }

    private class AgentTask
    {
        public string Text { get; set; } = "";
        public string? Type { get; set; }
        public Dictionary<string, int>? Points { get; set; }
    }

    private class AgentOutbox
    {
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public string Timestamp { get; set; } = "";
    }
}
