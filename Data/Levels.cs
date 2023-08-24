using System.Xml.Linq;
using System.Xml.XPath;

namespace BG3LootTableGenerator;

public partial class Levels
{
    public IReadOnlyDictionary<string, Entry> Entries => _entries;
    private readonly Dictionary<string, Entry> _entries = new();

    private readonly Localization _loc;
    private readonly Tags _tags;
    private readonly Dictionary<string, string> _levelLoc;

    public Levels(IEnumerable<string> loadOrder, Localization loc, Tags tags)
    {
        _loc = loc;
        _tags = tags;
        _levelLoc = LoadLevelLocalization(loadOrder);

        foreach (string level in loadOrder.SelectMany(GetLevelsInPath).Progress("Loading levels"))
        {
            Dictionary<string, EntryCharacter> characters = new();

            foreach (string directory in loadOrder)
            {
                string globalPath = Path.Combine(Config.SourceDir, directory, "Globals", level);
                string localPath = Path.Combine(Config.SourceDir, directory, "Levels", level);

                foreach (EntryCharacter character in LoadCharacters(new string[] { globalPath, localPath }))
                {
                    characters[character.MapKey] = character;
                }
            }

            string levelName = level;

            if (_levelLoc.TryGetValue(level, out string? resolvedLevelName))
            {
                levelName = resolvedLevelName;
            }
            
            _entries[level] = new(
                Name: levelName,
                Id: level,
                Characters: characters.Values);
        }
    }

    private IEnumerable<EntryCharacter> LoadCharacters(IEnumerable<string> paths)
    {
        IEnumerable<string> templatePaths = Enumerable.Empty<string>();

        foreach (string path in paths)
        {
            string characterPath = Path.Combine(path, "Characters");

            if (Directory.Exists(characterPath))
            {
                templatePaths = templatePaths.Concat(Util.GetAllTemplates(characterPath));
            }
        }

        foreach (string templatePath in templatePaths)
        {
            IEnumerable<XElement> elements = XDocument.Load(templatePath).Root!.XPathSelectElements(
                "region[@id = 'Templates']/node/children/node[@id = 'GameObjects'][attribute[@id='Type' and @value='character']]");

            foreach (XElement element in elements)
            {
                string? GetAttribute(string id, string name) => element.XPathSelectElement($"attribute[@id='{id}']")?.Attribute(name)?.Value;
                string? GetAttributeValue(string id) => GetAttribute(id, "value");
                string? GetAttributeHandle(string id) => GetAttribute(id, "handle");

                string? GetTransformPosition() => element
                    .XPathSelectElement($"children/node[@id = 'Transform']/attribute[@id='Position']")?
                    .Attribute("value")!.Value;

                IEnumerable<string> GetTreasureTables(string name) => element
                    .XPathSelectElements($"children/node[@id = '{name}']/children/node[@id = 'TreasureItem']/attribute")
                    .Select(x => x.Attribute("value")!.Value)
                    .Where(x => x != "Empty");

                string name = GetAttributeValue("Name")!;
                string mapKey = GetAttributeValue("MapKey")!;
                string templateName = GetAttributeValue("TemplateName")!;

                string? position = GetTransformPosition();
                string? displayName = GetAttributeHandle("DisplayName");

                if (displayName != null && _loc.Entries.TryGetValue(displayName, out string? resolvedDisplayName))
                {
                    displayName = resolvedDisplayName;
                }

                List<string> tags = new();

                foreach (XElement tagElement in element.XPathSelectElements("children/node[@id = 'Tags']/children/node[@id = 'Tag']/attribute"))
                {
                    string itemTagGuid = tagElement.Attribute("value")!.Value;
                    _tags.Entries.TryGetValue(itemTagGuid, out string? itemTag);
                    tags.Add(itemTag ?? itemTagGuid);
                }

                IEnumerable<string> treasureTables = GetTreasureTables("Treasures");
                IEnumerable<string> tradeTreasureTables = GetTreasureTables("TradeTreasures");

                yield return new(
                    Name: name,
                    MapKey: mapKey,
                    Template: templateName,
                    Position: position,
                    DisplayName: displayName,
                    Tags: tags.Any() ? tags : null,
                    TreasuresTables: treasureTables.Any() ? treasureTables : null,
                    TradeTreasureTables: tradeTreasureTables.Any() ? tradeTreasureTables : null);
            }
        }
    }

    private Dictionary<string, string> LoadLevelLocalization(IEnumerable<string> loadOrder)
    {
        Dictionary<string, string> loc = new();

        foreach (string directory in loadOrder)
        {
            string path = Path.Combine(Config.SourceDir, directory, "Localization", "Levels.lsx");

            if (File.Exists(path))
            {
                IEnumerable<XElement> elements = XDocument.Load(path).Root!.XPathSelectElements(
                    "region[@id = 'TranslatedStringKeys']/node/children/node[@id = 'TranslatedStringKey']");

                foreach (XElement element in elements)
                {
                    string? GetAttribute(string id, string name) => element.XPathSelectElement($"attribute[@id='{id}']")?.Attribute(name)?.Value;
                    string? GetAttributeValue(string id) => GetAttribute(id, "value");
                    string? GetAttributeHandle(string id) => GetAttribute(id, "handle");

                    string id = GetAttributeValue("UUID")!;
                    string handle = GetAttributeHandle("Content")!;

                    _loc.Entries.TryGetValue(handle, out string? localized);
                    loc[id] = localized ?? handle;
                }
            }
        }

        return loc;
    }
    
    private IEnumerable<string> GetLevelsInPath(string path)
        => Directory.EnumerateDirectories(Path.Combine(Config.SourceDir, path, "Globals"))
        .Concat(Directory.EnumerateDirectories(Path.Combine(Config.SourceDir, path, "Levels")))
        .Select(x => Path.GetFileName(x))
        .Distinct();
}

public partial class Levels
{
    public record Entry(
        string Name,
        string Id,
        IEnumerable<EntryCharacter> Characters);

    public record EntryCharacter(
        string Name,
        string MapKey,
        string Template,
        string? Position,
        string? DisplayName,
        IEnumerable<string>? Tags,
        IEnumerable<string>? TreasuresTables,
        IEnumerable<string>? TradeTreasureTables);
}
