using System.Xml.Linq;
using System.Xml.XPath;

namespace BG3LootTableGenerator;

public class Tags
{
    public IReadOnlyDictionary<string, string> Entries => _entries;
    private static Dictionary<string, string> _entries = new();

    public Tags(IEnumerable<string> loadOrder)
    {
        foreach (string filePath in loadOrder.SelectMany(x => Util.GetAllTemplates(Path.Combine(Config.SourceDir, x, "Tags"))))
        {
            IEnumerable<XElement> nodes = XDocument.Load(filePath).Root!.XPathSelectElements("region[@id = 'Tags']/node[@id = 'Tags']");
            foreach (XElement node in nodes)
            {
                string? GetAttributeValue(string id) => node.XPathSelectElement($"attribute[@id='{id}']")?.Attribute("value")?.Value;

                string? guid = GetAttributeValue("UUID");
                string? name = GetAttributeValue("Name");

                if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(name))
                {
                    Console.WriteLine($"Error (invalid tag) in {filePath}");
                    continue;
                }

                _entries[guid] = name;
            }
        }
    }
}
