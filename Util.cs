namespace BG3LootTableGenerator;

public static class Util
{
    public static IEnumerable<string> GetAllTemplates(string dir) => Directory
        .EnumerateFiles(Path.Combine(dir), "*.lsx", SearchOption.AllDirectories)
        .Select(x => x.Replace('\\', '/'));
}

