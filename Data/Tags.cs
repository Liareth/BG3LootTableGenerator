using System.Xml.Linq;
using System.Xml.XPath;

namespace BG3LootTableGenerator;

public class Tags
{
    public IReadOnlyDictionary<string, string> Entries => _entries;
    private readonly Dictionary<string, string> _entries = new();

    public Tags(IEnumerable<string> loadOrder)
    {
        foreach (string filePath in loadOrder
            .SelectMany(x => Util.GetAllTemplates(Path.Combine(Config.SourceDir, x, "Tags")))
            .Progress("Loading tags"))
        {
            IEnumerable<XElement> elements = XDocument.Load(filePath).Root!.XPathSelectElements("region[@id = 'Tags']/node[@id = 'Tags']");
            foreach (XElement element in elements)
            {
                string? GetAttributeValue(string id) => element.XPathSelectElement($"attribute[@id='{id}']")?.Attribute("value")?.Value;
                _entries[GetAttributeValue("UUID")!] = GetAttributeValue("Name")!;
            }
        }
    }
}
