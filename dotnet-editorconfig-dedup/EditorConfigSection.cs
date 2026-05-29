namespace dotnet_editorconfig_dedup;

public class EditorConfigSection
{
    public string Pattern { get; set; }
    public List<PropertyDefinition> Properties { get; private set; }
    public int LineNumber { get; set; }

    public EditorConfigSection(string pattern, int lineNumber)
    {
        Pattern = pattern;
        LineNumber = lineNumber;
        Properties = new List<PropertyDefinition>();
    }

    public void AddProperty(PropertyDefinition property)
    {
        Properties.Add(property);
    }

    public override string ToString() => $"{Pattern} with {Properties.Count} properties";
}
