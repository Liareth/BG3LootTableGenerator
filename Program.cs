using BG3LootTableGenerator;
using CommandLine;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.Json.Serialization;

Parser.Default.ParseArguments<CmdlineOptions>(args).WithParsed(Run);

void Run(CmdlineOptions options)
{
    Config.SourceDir = options.SourceDir.Replace('\\', '/');
    Config.DestinationDir = options.DestinationDir.Replace('\\', '/');
    Config.WriteProofOfConcept = options.WriteProofOfConcept;

    Localization localization = new("English/Localization/English/english.xml");

    List<string> templateLoadOrder = new() {
        "Shared/Public/Shared",
        "Shared/Public/SharedDev",
        "Gustav/Public/Gustav",
        "Gustav/Public/GustavDev"
    };

    Tags tags = new(templateLoadOrder);
    Items items = new(templateLoadOrder, localization, tags);

    List<string> levelLoadOrder = new(){
        "Gustav/Mods/Gustav",
        "Gustav/Mods/GustavDev"
    };

    Levels levels = new(levelLoadOrder, localization, tags);

    SaveOutput(new(items, levels, localization, tags));
}

void SaveOutput(Data data)
{
    JsonSerializerOptions opts = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    List<Action<Data>> saving = new()
    {
        x => WriteResourceList(x.Items, opts),
        x => WriteLevels(x.Levels, opts),
        x => WriteTraders(x.Levels, opts)
    };

    if (Config.WriteProofOfConcept)
    {
        saving.Add(x => WriteProofOfConceptMod(x.Items));
    }

    foreach (Action<Data> fn in saving.Progress("Saving output", TimeSpan.Zero))
    {
        fn(data);
    }
}

void WriteResourceList(Items items, JsonSerializerOptions opts)
{
    string path = Path.Combine(Config.DestinationDir, "items.json");
    Util.SaveToFile(path, JsonSerializer.Serialize(items.Entries.Values, opts));
}
void WriteLevels(Levels levels, JsonSerializerOptions opts)
{
    string path = Path.Combine(Config.DestinationDir, "levels.json");
    Util.SaveToFile(path, JsonSerializer.Serialize(levels.Entries.Values, opts));
}

void WriteTraders(Levels levels, JsonSerializerOptions opts)
{
    string path = Path.Combine(Config.DestinationDir, "traders.json");
    Util.SaveToFile(path, JsonSerializer.Serialize(levels.Entries.Values
        .Select(l => new TraderLevel(
            LevelName: l.Name,
            LevelId: l.Id,
            Traders: l.Characters
                .Where(c => c.Tags?.Contains("TRADER") ?? false)
                .Select(c => new TraderCharacter(
                    TraderName: c.DisplayName,
                    TraderId: c.Name,
                    TraderTreasureTables: c.TradeTreasureTables!))))
        .Where(l => l.Traders.Any()), opts));
}

void WriteProofOfConceptMod(Items items)
{
    IEnumerable<Items.Entry> armours = items.Entries.Values.Where(x => x.InheritsFrom("BASE_ARMOR"));
    IEnumerable<Items.Entry> armoursWithStats = armours.Where(x => !string.IsNullOrWhiteSpace(x.Data.Stats));
    IEnumerable<Items.Entry> armoursWithoutStats = armours.Except(armoursWithStats);
    List<string> armourStatNames = armoursWithStats.Select(x => x.Data.Stats!).ToList();

    List<string> generatedArmourTxt = new();
    foreach (Items.Entry armour in armoursWithoutStats)
    {
        string statsName = $"LIA_GENERATED_{armour.Name}";
        string? inheritedStats = armour.GetStats(items.Entries.Values);
        if (!string.IsNullOrWhiteSpace(inheritedStats))
        {
            generatedArmourTxt.Add(
                $"new entry \"{statsName}\"\n" +
                $"using \"{armour.GetStats(items.Entries.Values)}\"\n" +
                $"data \"RootTemplate\" \"{armour.MapKey}\"\n" +
                $"data \"Unique\" \"0\"\n");

            armourStatNames.Add(statsName);
        }
    }

    List<string> generatedTreasureTxt = new() { "new treasuretable \"TUT_Chest_Potions\"\nCanMerge 1" };
    generatedTreasureTxt.AddRange(armourStatNames.Select(x => $"new subtable \"1,1\"\nobject category \"I_{x}\",1,0,0,0,0,0,0,0"));

    Util.SaveToFile(Path.Combine(Config.DestinationDir, "Armour.txt"), generatedArmourTxt);
    Util.SaveToFile(Path.Combine(Config.DestinationDir, "TreasureTable.txt"), generatedTreasureTxt);
}

record Data(Items Items, Levels Levels, Localization Localization, Tags Tags);
record TraderCharacter(string? TraderName, string TraderId, IEnumerable<string> TraderTreasureTables);
record TraderLevel(string LevelName, string LevelId, IEnumerable<TraderCharacter> Traders);

class CmdlineOptions
{
    [Value(0, MetaName = "source", HelpText = "Source directory (needs Shared and Gustav unpacked)", Required = true)]
    public required string SourceDir { get; set; }

    [Value(1, MetaName = "destination", HelpText = "Where to store written files?", Required = true)]
    public required string DestinationDir { get; set; }

    [Option("region", HelpText = "Whether we should write the proof of concept armour mod or not.")]
    public bool WriteProofOfConcept { get; set; } = false;
}
