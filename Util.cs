namespace BG3LootTableGenerator;

public static class Util
{
    public static IEnumerable<string> GetAllTemplates(string dir) 
        => Directory.EnumerateFiles(Path.Combine(dir), "*.lsx", SearchOption.AllDirectories)
        .Select(x => x.Replace('\\', '/'));

    public static void SaveToFile(string path, string data)
    {
        File.WriteAllText(path, data);
        Console.WriteLine($"Saved {Path.GetFileName(path)} to {Path.GetDirectoryName(path)}");
    }

    public static void SaveToFile(string path, IEnumerable<string> data) 
        => SaveToFile(path, string.Join('\n', data));
}

public class ProgressTracker : IDisposable
{
    private readonly string _label;
    private readonly TimeSpan _interval;
    private readonly DateTimeOffset _start = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;

    public ProgressTracker(string label, TimeSpan? interval = null)
    {
        _label = label;
        _interval = interval ?? TimeSpan.FromMilliseconds(100);
    }

    public void Update(int current, int total)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now.Subtract(_lastUpdate) >= _interval)
        {
            Console.WriteLine($"{GetElapsedTime()} {_label} {Math.Floor((double)current / total * 100):F0}% ({current}/{total})");
            _lastUpdate = now;
        }
    }

    public void Dispose()
    {
        Console.WriteLine($"{GetElapsedTime()} {_label} finished");
    }

    private string GetElapsedTime()
        => $"[+{(DateTimeOffset.UtcNow - _start).TotalSeconds:F2}s]";
}

public static class ProgressTrackerExtension
{
    public static IEnumerable<T> Progress<T>(this IEnumerable<T> enumerable, string label, TimeSpan? interval = null)
    {
        using ProgressTracker? tracker = new(label, interval);

        int total = enumerable.Count();
        int current = 0;

        foreach (T item in enumerable)
        {
            tracker.Update(current++, total);
            yield return item;
        }
    }
}
