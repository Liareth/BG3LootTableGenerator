using System.Xml.Linq;
using System.Xml.XPath;

namespace BG3LootTableGenerator;

public class Localization
{
    public IReadOnlyDictionary<string, string> Entries => _entries;
    private readonly Dictionary<string, string> _entries = new();

    public Localization(string path)
    {
        foreach (XElement elem in XDocument
            .Load(Path.Combine(Config.SourceDir, path))
            .XPathSelectElements("contentList/content")
            .Progress("Loading localization"))
        {
            _entries[elem.Attribute("contentuid")!.Value] = elem.Value;
        }
    }
}
