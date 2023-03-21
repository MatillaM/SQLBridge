namespace SQLBridge.OracleToDotNet.Classes;

internal class TableProperty
{
    public TableProperty(string name, string type, string length, string comment)
    {
        Name = name;
        Type = type;
        Comment = comment;
        Length = string.IsNullOrEmpty(length) || !float.TryParse(length, out float l) ? null : l;
    }

    internal string Name { get; set; }
    internal string Type { get; set; }
    internal string Comment { get; set; }
    internal float? Length { get; set; }
}