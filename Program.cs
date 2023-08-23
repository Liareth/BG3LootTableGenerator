using CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Xml.XPath;

Dictionary<string, ItemEntry> _entries = new();
Dictionary<string, string> _localization = new();
Dictionary<string, string> _tags = new();

Parser.Default.ParseArguments<CmdlineOptions>(args).WithParsed(Run);

void Run(CmdlineOptions options)
{
    options.SourceDir = options.SourceDir.Replace('\\', '/');
    options.DestinationDir = options.DestinationDir.Replace('\\', '/');

    IEnumerable<string> templates = GetAllTemplates(Path.Combine(options.SourceDir, "Shared/Public/Shared"))
        .Concat(GetAllTemplates(Path.Combine(options.SourceDir, "Shared/Public/SharedDev")))
        .Concat(GetAllTemplates(Path.Combine(options.SourceDir, "Gustav/Public/Gustav")))
        .Concat(GetAllTemplates(Path.Combine(options.SourceDir, "Gustav/Public/GustavDev")))
        .ToList();

    IEnumerable<string> rootTemplates = templates.Where(x => Path.GetDirectoryName(x)!.EndsWith("RootTemplates"));
    IEnumerable<string> tagTemplates = templates.Where(x => Path.GetDirectoryName(x)!.EndsWith("Tags"));

    LoadLocalization(options);
    LoadTags(options, tagTemplates);
    LoadRootTemplates(options, rootTemplates);

    PopulateResolvedData();

    File.WriteAllText(
        Path.Combine(options.DestinationDir, "items.json"), 
        JsonSerializer.Serialize(_entries.Values, options: new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    }));

    IEnumerable<ItemEntry> armours = _entries.Values.Where(x => x.InheritsFrom("BASE_ARMOR"));
    IEnumerable<ItemEntry> armoursWithStats = armours.Where(x => !string.IsNullOrWhiteSpace(x.Data.Stats));
    IEnumerable<ItemEntry> armoursWithoutStats = armours.Except(armoursWithStats);
    List<string> armourStatNames = armoursWithStats.Select(x => x.Data.Stats!).ToList();

    List<string> generatedArmourTxt = new();
    foreach (ItemEntry armour in armoursWithoutStats)
    {
        string statsName = $"LIA_GENERATED_{armour.Name}";
        string? inheritedStats = armour.GetStats(_entries.Values);
        if (!string.IsNullOrWhiteSpace(inheritedStats))
        {
            generatedArmourTxt.Add(
                $"new entry \"{statsName}\"\n" +
                $"using \"{armour.GetStats(_entries.Values)}\"\n" +
                $"data \"RootTemplate\" \"{armour.MapKey}\"\n" +
                $"data \"Unique\" \"0\"\n");

            armourStatNames.Add(statsName);
        }
    }

    List<string> generatedTreasureTxt = new() { "new treasuretable \"TUT_Chest_Potions\"\nCanMerge 1" };
    generatedTreasureTxt.AddRange(armourStatNames.Select(x => $"new subtable \"1,1\"\nobject category \"I_{x}\",1,0,0,0,0,0,0,0"));

    File.WriteAllLines(Path.Combine(options.DestinationDir, "Armour.txt"), generatedArmourTxt);
    File.WriteAllLines(Path.Combine(options.DestinationDir, "TreasureTable.txt"), generatedTreasureTxt);
}

void LoadLocalization(CmdlineOptions options)
{
    foreach (XElement elem in XDocument.Load(Path.Combine(options.SourceDir, "English/Localization/English/english.xml")).XPathSelectElements("contentList/content"))
    {
        _localization[elem.Attribute("contentuid")!.Value] = elem.Value;
    }
}

void LoadTags(CmdlineOptions options, IEnumerable<string> files)
{
    foreach (string filePath in files)
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

            _tags[guid] = name;
        }
    }
}

void LoadRootTemplates(CmdlineOptions options, IEnumerable<string> files)
{
    foreach (string filePath in files)
    {
        IEnumerable<XElement> nodes = XDocument.Load(filePath).Root!.XPathSelectElements("region[@id = 'Templates']/node/children/node[@id = 'GameObjects'][attribute[@id='Type' and @value='item']]");
        foreach (XElement node in nodes)
        {
            string? GetAttribute(string id, string name) => node.XPathSelectElement($"attribute[@id='{id}']")?.Attribute(name)?.Value;
            string? GetAttributeValue(string id) => GetAttribute(id, "value");
            string? GetAttributeHandle(string id) => GetAttribute(id, "handle");

            string? mapKey = GetAttributeValue("MapKey");
            string? name = GetAttributeValue("Name");

            if (string.IsNullOrEmpty(mapKey) || string.IsNullOrEmpty(name))
            {
                Console.WriteLine($"Error (invalid root template) in {filePath}");
                continue;
            }

            string? parent = GetAttributeValue("ParentTemplateId");

            string? displayName = GetAttributeHandle("DisplayName");
            if (displayName != null) _localization.TryGetValue(displayName, out displayName);

            string? technicalDescription = GetAttributeHandle("TechnicalDescription");
            if (technicalDescription != null) _localization.TryGetValue(technicalDescription, out technicalDescription);

            string? description = GetAttributeHandle("Description");
            if (description != null) _localization.TryGetValue(description, out description);

            List<string> tags = new();

            foreach (XElement tagNode in node.XPathSelectElements("children/node[@id = 'Tags']/children/node[@id = 'Tag']/attribute"))
            {
                string tagGuid = tagNode.Attribute("value")!.Value;
                _tags.TryGetValue(tagGuid, out string? tag);
                tags.Add(tag ?? tagGuid);
            }

            ItemEntry entry = new(
                Name: name,
                MapKey: mapKey,
                Path: filePath.Replace(options.SourceDir, "").TrimStart('/'),
                Data: new(
                    DisplayName: displayName,
                    TechnicalDescription: technicalDescription,
                    Description: description,
                    ParentTemplateId: GetAttributeValue("ParentTemplateId"),
                    VisualTemplateId: GetAttributeValue("VisualTemplate"),
                    Stats: GetAttributeValue("Stats"),
                    Icon: GetAttributeValue("Icon"),
                    Tags: tags.Any() ? tags : null),
                ResolvedData: default!);

            _entries[mapKey] = entry;
        }
    }
}

void PopulateResolvedData()
{
    foreach ((string k, ItemEntry v) in _entries)
    {
        List<string> inheritance = new();
        List<string> tags = new();

        ItemEntry entry = _entries[k];

        string currentMapKey = k;
        string? currentParentMapKey = v.Data.ParentTemplateId;

        tags.AddRange(entry.Data.Tags ?? Enumerable.Empty<string>());

        if (!string.IsNullOrWhiteSpace(currentParentMapKey) && currentMapKey != currentParentMapKey)
        {
            do
            {
                _entries.TryGetValue(currentParentMapKey, out ItemEntry? currentParent);

                if (currentParent == null)
                {
                    inheritance.Add(currentParentMapKey);
                    break; // This could happen due to invalid data / not parsing it all
                }

                currentMapKey = currentParentMapKey;
                currentParentMapKey = currentParent.Data.ParentTemplateId;

                inheritance.Add(currentParent.Name);
                tags.AddRange(currentParent.Data.Tags ?? Enumerable.Empty<string>());
            } while (!string.IsNullOrWhiteSpace(currentParentMapKey) && currentMapKey != currentParentMapKey);
        }

        ItemEntryResolvedData data = new(
            Parents: inheritance.Any() ? inheritance.Distinct() : null,
            Tags: tags.Any() ? tags.Distinct() : null);

        _entries[k] = entry with { ResolvedData = data };
    }
}

IEnumerable<string> GetAllTemplates(string dir)
{
    return Directory
        .EnumerateFiles(Path.Combine(dir), "*.lsx", SearchOption.AllDirectories)
        .Select(x => x.Replace('\\', '/'));
}

record ItemEntry(
    string Name,
    string MapKey,
    string Path,
    ItemEntryData Data,
    ItemEntryResolvedData ResolvedData)
{
    public bool InheritsFrom(string name) => ResolvedData.Parents?.Any(x => x == name) ?? false;

    public string? GetStats(IEnumerable<ItemEntry> entries)
    {
        if (!string.IsNullOrWhiteSpace(Data.Stats))
        {
            return Data.Stats;
        }

        foreach (string candidate in ResolvedData.Parents ?? Enumerable.Empty<string>())
        {
            ItemEntry? parent = entries.First(x => candidate == x.Name); // TODO: Should be MapKey to be safe?
            if (!string.IsNullOrWhiteSpace(parent.Data.Stats)) return parent.Data.Stats;
        }

        Console.WriteLine($"Warning: Couldn't find stats for {Name} / {MapKey}");
        return null;
    }
}

record ItemEntryResolvedData(
    IEnumerable<string>? Parents,
    IEnumerable<string>? Tags);

record ItemEntryData(
    string? DisplayName,
    string? Description,
    string? TechnicalDescription,
    string? ParentTemplateId,
    string? VisualTemplateId,
    string? Stats,
    string? Icon,
    IEnumerable<string>? Tags);

class CmdlineOptions
{
    [Value(0, MetaName = "source", HelpText = "Source directory (needs Shared and Gustav unpacked)", Required = true)]
    public required string SourceDir { get; set; }

    [Value(1, MetaName = "destination", HelpText = "Where to store written files?", Required = true)]
    public required string DestinationDir { get; set; }
}
