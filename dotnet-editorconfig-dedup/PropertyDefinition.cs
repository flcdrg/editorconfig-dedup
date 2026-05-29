namespace dotnet_editorconfig_dedup;

public class PropertyDefinition
{
    public string Key { get; set; }
    public string Value { get; set; }
    public int LineNumber { get; set; }
    public bool IsRedundant { get; set; }

    public PropertyDefinition(string key, string value, int lineNumber)
    {
        Key = key;
        Value = value;
        LineNumber = lineNumber;
        IsRedundant = false;
    }

    public override string ToString() => $"{Key} = {Value}";
}
