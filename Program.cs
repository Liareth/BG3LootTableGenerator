using CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Xml.XPath;

Dictionary<string, ItemEntry> _entries = new();
Dictionary<string, string> _localization = new();

Parser.Default.ParseArguments<CmdlineOptions>(args).WithParsed(Run);

void Run(CmdlineOptions options)
{
    options.SourceDir = options.SourceDir.Replace('\\', '/');
    options.DestinationDir = options.DestinationDir.Replace('\\', '/');

    foreach (XElement elem in XDocument.Load(Path.Combine(options.SourceDir, "English/Localization/English/english.xml")).XPathSelectElements("contentList/content"))
    {
        _localization[elem.Attribute("contentuid")!.Value] = elem.Value;
    }

    List<string> files = GetAllRootTemplates(Path.Combine(options.SourceDir, "Shared/Public/Shared"))
        .Concat(GetAllRootTemplates(Path.Combine(options.SourceDir, "Shared/Public/SharedDev")))
        .Concat(GetAllRootTemplates(Path.Combine(options.SourceDir, "Gustav/Public/Gustav")))
        .Concat(GetAllRootTemplates(Path.Combine(options.SourceDir, "Gustav/Public/GustavDev")))
        .ToList();

    for (int i = 0; i < files.Count; ++i)
    {
        string filePath = files[i];
        string relativeFilePath = filePath.Replace(options.SourceDir, "").TrimStart('/');
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
                Console.WriteLine($"Error in {filePath}");
                continue;
            }

            string? parent = GetAttributeValue("ParentTemplateId");

            string? displayName = GetAttributeHandle("DisplayName");
            if (displayName != null) _localization.TryGetValue(displayName, out displayName);

            string? technicalDescription = GetAttributeHandle("TechnicalDescription");
            if (technicalDescription != null) _localization.TryGetValue(technicalDescription, out technicalDescription);

            string? description = GetAttributeHandle("Description");
            if (description != null) _localization.TryGetValue(description, out description);

            ItemEntry entry = new(
                Name: name,
                Inheritance: null, // later
                MapKey: mapKey,
                Path: relativeFilePath,
                Data: new(
                    ParentTemplateId: GetAttributeValue("ParentTemplateId"),
                    VisualTemplateId: GetAttributeValue("VisualTemplate"),
                    Stats: GetAttributeValue("Stats"),
                    Icon: GetAttributeValue("Icon")),
                Localization: new(
                    DisplayName: displayName,
                    TechnicalDescription: technicalDescription,
                    Description: description));

            _entries[mapKey] = entry;
        }
    }

    foreach ((string k, ItemEntry v) in _entries)
    {
        string currentMapKey = k;
        string? currentParentMapKey = v.Data.ParentTemplateId;
        List<string> inheritance = new() { _entries[k].Name };

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
            } while (!string.IsNullOrWhiteSpace(currentParentMapKey) && currentMapKey != currentParentMapKey);

            if (inheritance.Count > 1)
            {
                inheritance.Reverse();
                _entries[k] = _entries[k] with { Inheritance = string.Join(':', inheritance) };
            }
        }
    }

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
        generatedArmourTxt.Add($"new entry \"{statsName}\"\ndata \"RootTemplate\" \"{armour.MapKey}\"\n");
        armourStatNames.Add(statsName);
    }

    List<string> generatedTreasureTxt = new() { "new treasuretable \"TUT_Chest_Potions\"\nCanMerge 1" };
    generatedTreasureTxt.AddRange(armourStatNames.Select(x => $"new subtable \"1,1\"\nobject category \"I_{x}\",1,0,0,0,0,0,0,0"));

    File.WriteAllLines(Path.Combine(options.DestinationDir, "Armour.txt"), generatedArmourTxt);
    File.WriteAllLines(Path.Combine(options.DestinationDir, "TreasureTable.txt"), generatedTreasureTxt);
}

IEnumerable<string> GetAllRootTemplates(string dir)
{
    return Directory
        .EnumerateFiles(Path.Combine(dir), "*.lsx", SearchOption.AllDirectories)
        .Where(x => Path.GetDirectoryName(x)!.EndsWith("RootTemplates"))
        .Select(x => x.Replace('\\', '/'));
}

record ItemEntry(string Name, string? Inheritance, string MapKey, string Path, ItemEntryData Data, ItemEntryLocalization Localization)
{
    public bool InheritsFrom(string name) => Inheritance?.Split(':').Any(x => x == name) ?? false;
}

record ItemEntryData(string? ParentTemplateId, string? VisualTemplateId, string? Stats, string? Icon);
record ItemEntryLocalization(string? DisplayName, string? TechnicalDescription, string? Description);

class CmdlineOptions
{
    [Value(0, MetaName = "source", HelpText = "Source directory (needs Shared and Gustav unpacked)", Required = true)]
    public required string SourceDir { get; set; }

    [Value(1, MetaName = "destination", HelpText = "Where to store written files?", Required = true)]
    public required string DestinationDir { get; set; }
}
