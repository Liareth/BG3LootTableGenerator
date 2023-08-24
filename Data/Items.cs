using System.Xml.Linq;
using System.Xml.XPath;

namespace BG3LootTableGenerator;

public partial class Items
{
    public IReadOnlyDictionary<string, Entry> Entries => _entries;
    private readonly Dictionary<string, Entry> _entries = new();

    public Items(IEnumerable<string> loadOrder, Localization localization, Tags tags)
    {
        // First pass: load all items, and their data, from root templates.
        foreach (string filePath in loadOrder
            .SelectMany(x => Util.GetAllTemplates(Path.Combine(Config.SourceDir, x, "RootTemplates")))
            .Progress("Loading item templates"))
        {
            IEnumerable<XElement> elements = XDocument.Load(filePath).Root!.XPathSelectElements(
                "region[@id = 'Templates']/node/children/node[@id = 'GameObjects'][attribute[@id='Type' and @value='item']]");

            foreach (XElement element in elements)
            {
                string? GetAttribute(string id, string name) => element.XPathSelectElement($"attribute[@id='{id}']")?.Attribute(name)?.Value;
                string? GetAttributeValue(string id) => GetAttribute(id, "value");
                string? GetAttributeHandle(string id) => GetAttribute(id, "handle");

                string mapKey = GetAttributeValue("MapKey")!;
                string name = GetAttributeValue("Name")!;
                string? parent = GetAttributeValue("ParentTemplateId");
                string? displayName = GetAttributeHandle("DisplayName");
                string? technicalDescription = GetAttributeHandle("TechnicalDescription");
                string? description = GetAttributeHandle("Description");

                if (displayName != null) localization.Entries.TryGetValue(displayName, out displayName);
                if (technicalDescription != null) localization.Entries.TryGetValue(technicalDescription, out technicalDescription);
                if (description != null) localization.Entries.TryGetValue(description, out description);

                List<string> itemTags = new();

                foreach (XElement tagElement in element.XPathSelectElements("children/node[@id = 'Tags']/children/node[@id = 'Tag']/attribute"))
                {
                    string itemTagGuid = tagElement.Attribute("value")!.Value;
                    tags.Entries.TryGetValue(itemTagGuid, out string? itemTag);
                    itemTags.Add(itemTag ?? itemTagGuid);
                }

                Entry entry = new(
                    Name: name,
                    MapKey: mapKey,
                    Path: filePath.Replace(Config.SourceDir, "").TrimStart('/'),
                    Data: new(
                        DisplayName: displayName,
                        TechnicalDescription: technicalDescription,
                        Description: description,
                        ParentTemplateId: GetAttributeValue("ParentTemplateId"),
                        VisualTemplateId: GetAttributeValue("VisualTemplate"),
                        Stats: GetAttributeValue("Stats"),
                        Icon: GetAttributeValue("Icon"),
                        Tags: itemTags.Any() ? itemTags : null),
                    ResolvedData: default!);

                _entries[mapKey] = entry;
            }
        }

        // Second pass: modify every item to add resolved data using data from other items.
        foreach ((string k, Entry v) in _entries.Progress("Resolving item data"))
        {
            List<string> itemParents = new();
            List<string> itemTags = new();

            Entry entry = _entries[k];

            string currentMapKey = k;
            string? currentParentMapKey = v.Data.ParentTemplateId;

            itemTags.AddRange(entry.Data.Tags ?? Enumerable.Empty<string>());

            if (!string.IsNullOrWhiteSpace(currentParentMapKey) && currentMapKey != currentParentMapKey)
            {
                do
                {
                    _entries.TryGetValue(currentParentMapKey, out Entry? currentParent);

                    if (currentParent == null)
                    {
                        itemParents.Add(currentParentMapKey);
                        break; // This could happen due to invalid data / not parsing it all
                    }

                    currentMapKey = currentParentMapKey;
                    currentParentMapKey = currentParent.Data.ParentTemplateId;

                    itemParents.Add(currentParent.Name);
                    itemTags.AddRange(currentParent.Data.Tags ?? Enumerable.Empty<string>());
                } while (!string.IsNullOrWhiteSpace(currentParentMapKey) && currentMapKey != currentParentMapKey);
            }

            EntryResolvedData data = new(
                Parents: itemParents.Any() ? itemParents.Distinct() : null,
                Tags: itemTags.Any() ? itemTags.Distinct() : null);

            _entries[k] = entry with { ResolvedData = data };
        }
    }
}

public partial class Items
{
    public record Entry(
    string Name,
    string MapKey,
    string Path,
    EntryData Data,
    EntryResolvedData ResolvedData)
    {
        public bool InheritsFrom(string name) => ResolvedData.Parents?.Any(x => x == name) ?? false;

        public string? GetStats(IEnumerable<Entry> entries)
        {
            if (!string.IsNullOrWhiteSpace(Data.Stats))
            {
                return Data.Stats;
            }

            foreach (string candidate in ResolvedData.Parents ?? Enumerable.Empty<string>())
            {
                Entry? parent = entries.First(x => candidate == x.Name); // TODO: Should be MapKey to be safe?
                if (!string.IsNullOrWhiteSpace(parent.Data.Stats)) return parent.Data.Stats;
            }

            Console.WriteLine($"Warning: Couldn't find stats for {Name} / {MapKey}");
            return null;
        }
    }

    public record EntryData(
        string? DisplayName,
        string? Description,
        string? TechnicalDescription,
        string? ParentTemplateId,
        string? VisualTemplateId,
        string? Stats,
        string? Icon,
        IEnumerable<string>? Tags);

    public record EntryResolvedData(
        IEnumerable<string>? Parents,
        IEnumerable<string>? Tags);
}
