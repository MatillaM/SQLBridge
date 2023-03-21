namespace SQLBridge.OracleToDotNet.Classes;

internal class TableEntity
{
    public TableEntity(string name, IEnumerable<TableProperty> properties, IEnumerable<string> indexes, IEnumerable<string> triggers, string script)
    {
        Name = name;
        Properties = properties;
        Indexes = indexes;
        Triggers = triggers.Select(t => Utils.RemoveEmptyLines(t));
        Script = Utils.RemoveEmptyLines(script);
    }

    internal string Name { get; set; }
    internal IEnumerable<TableProperty> Properties { get; set; }
    internal IEnumerable<string> Indexes { get; set; }
    internal IEnumerable<string> Triggers { get; set; }
    internal string Script { get; set; }
}
