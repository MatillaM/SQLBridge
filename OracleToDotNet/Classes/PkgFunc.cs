namespace SQLBridge.OracleToDotNet.Classes;

internal class PkgFunc
{
    internal string Name { get; set; }
    internal string Script { get; set; }

    internal IEnumerable<string> Calls { get; set; }
    internal IEnumerable<string> InternalCalls { get; set; }
    internal IEnumerable<string> ExternalCalls { get; set; }
    internal IEnumerable<string> TablesInSelects { get; set; }
    internal IEnumerable<string> TablesInInserts { get; set; }
    internal IEnumerable<string> TablesInUpdates { get; set; }
    internal IEnumerable<string> TablesInDeletes { get; set; }
    internal PkgFunc(string name, string script, IEnumerable<string> tablesInSelects, IEnumerable<string> tablesInInserts, IEnumerable<string> tablesInUpdates, IEnumerable<string> tablesInDeletes, IEnumerable<string> calls)
    {
        Name = name;
        Script = Utils.RemoveEmptyLines(script);
        TablesInSelects = tablesInSelects;
        TablesInInserts = tablesInInserts;
        TablesInUpdates = tablesInUpdates;
        TablesInDeletes = tablesInDeletes;
        Calls = calls;
        InternalCalls = Enumerable.Empty<string>();
        ExternalCalls = Enumerable.Empty<string>();
    }

}