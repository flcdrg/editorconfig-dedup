namespace dotnet_editorconfig_dedup;

public class DeduplicationSummary
{
    public Dictionary<string, FileDuplicates> DuplicatesByFile { get; set; } = new();
    public int TotalLinesRemoved { get; set; }

    public void AddDuplicate(string filePath, string section, string key, string value)
    {
        if (!DuplicatesByFile.ContainsKey(filePath))
        {
            DuplicatesByFile[filePath] = new FileDuplicates { FilePath = filePath };
        }

        if (!DuplicatesByFile[filePath].Duplicates.ContainsKey(section))
        {
            DuplicatesByFile[filePath].Duplicates[section] = new List<PropertyDuplicate>();
        }

        DuplicatesByFile[filePath].Duplicates[section].Add(new PropertyDuplicate { Key = key, Value = value });
    }

    public string GenerateReport()
    {
        if (DuplicatesByFile.Count == 0)
            return "No duplicates found.";

        var lines = new List<string> { "Deduplication Summary:", "" };

        foreach (var (filePath, duplicates) in DuplicatesByFile)
        {
            lines.Add($"File: {filePath}");
            foreach (var (section, props) in duplicates.Duplicates)
            {
                lines.Add($"  {section}");
                foreach (var prop in props)
                {
                    lines.Add($"    - {prop.Key} = {prop.Value}");
                }
            }
            lines.Add("");
        }

        lines.Add($"Total duplicates found: {TotalLinesRemoved}");
        return string.Join(Environment.NewLine, lines);
    }
}

public class FileDuplicates
{
    public string FilePath { get; set; } = string.Empty;
    public Dictionary<string, List<PropertyDuplicate>> Duplicates { get; set; } = new();
}

public class PropertyDuplicate
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
