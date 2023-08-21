# BG3LootTableGenerator
Dumps all items to .json, and creates treasure tables for armours.

Invoke like: `BGLootTableGenerator.exe [path_to_unpacked_content] [path_to_output_dir]`, for example: `BGLootTableGenerator.exe "C:\Users\Lia\Desktop\BG3\ModdersMultitool\UnpackedData" "C:\Users\Lia\Desktop\BG3\MyMods\.dev\.generation"`

You'll get:

- `items.json`: every item, formatted in a much more searchable/parseable way
- `Armour.txt`: armour stat entries for where an armour did not have a stat entry already
- `TreasureTable.txt`: generated treasure table adding to Nautiloid tutorial chest 
