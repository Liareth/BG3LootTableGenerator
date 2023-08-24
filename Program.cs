using BG3LootTableGenerator;
using CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

Parser.Default.ParseArguments<CmdlineOptions>(args).WithParsed(Run);

void Run(CmdlineOptions options)
{
    Config.SourceDir = options.SourceDir.Replace('\\', '/');
    Config.DestinationDir = options.DestinationDir.Replace('\\', '/');

    Localization localization = new("English/Localization/English/english.xml");

    List<string> loadOrder = new() {
        "Shared/Public/Shared",
        "Shared/Public/SharedDev",
        "Gustav/Public/Gustav",
        "Gustav/Public/GustavDev"
    };

    Tags tags = new(loadOrder);
    Items items = new(loadOrder, localization, tags);

    WriteResourceList(items);
    WriteProofOfConceptMod(items);
}

void WriteResourceList(Items items)
{
    File.WriteAllText(
        Path.Combine(Config.DestinationDir, "items.json"),
        JsonSerializer.Serialize(items.Entries.Values, options: new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
        }));
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

    File.WriteAllLines(Path.Combine(Config.DestinationDir, "Armour.txt"), generatedArmourTxt);
    File.WriteAllLines(Path.Combine(Config.DestinationDir, "TreasureTable.txt"), generatedTreasureTxt);
}

public class CmdlineOptions
{
    [Value(0, MetaName = "source", HelpText = "Source directory (needs Shared and Gustav unpacked)", Required = true)]
    public required string SourceDir { get; set; }

    [Value(1, MetaName = "destination", HelpText = "Where to store written files?", Required = true)]
    public required string DestinationDir { get; set; }
}
