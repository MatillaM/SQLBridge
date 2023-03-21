using System.Text;
using System.Text.RegularExpressions;
using SQLBridge.OracleToDotNet.Classes;

namespace SQLBridge.OracleToDotNet;

internal class ProcessEntities
{
    #region Consts
    const string CREATE = "CREATE";
    const string TABLE = "Table";
    const string TABLES = "Tables";
    const string VIEW = "View";
    const string VIEWS = "Views";
    const string INDEX = "Index";
    const string TRIGGER = "Trigger";
    const string BLOCKPREFIX = "DDL for";
    const string BYTE = "BYTE";
    const string DATE = "DATE";
    const string NUMBER = "NUMBER";
    const string IS = "IS";
    const string COLCOMMENT = "COMMENT ON COLUMN";
    const string TRIGGERPATTERN = "REFERENCING|DECLARE|FOR\\sEACH|BEGIN";
    public static readonly Dictionary<string, string> SQLToNetDataTypes = new Dictionary<string, string>
    {
        {"CHAR", "string?"},
        {"VARCHAR2", "string?"},
        {"NCHAR", "string?"},
        {"NVARCHAR2", "string?"},
        {"LONG", "string?"},
        {"NUMBER", "decimal"},
        {"FLOAT", "double"},
        {"DATE", "DateTime"},
        {"TIMESTAMP", "DateTime"},
        {"TIMESTAMP WITH TIME ZONE", "DateTimeOffset"},
        {"SYS.XMLTYPE", "string?"},
        {"CLOB", "string?"},
        {"BLOB", "byte[]"},
        {"RAW", "byte[]"},
        {"BOOLEAN", "bool"}
    };
    #endregion

    private readonly string SchemaName;

    internal ProcessEntities(string schemaEntitiesFilePath, string entitiesPath, string schemaName)
    {
        SchemaName = schemaName;

        var entitiesFile = Utils.RemoveComments(File.ReadAllText(schemaEntitiesFilePath, Encoding.Latin1));

        var (tables, views) = ExtractInformation(entitiesFile);

        var entitiesFolder = Directory.Exists(entitiesPath) ? new DirectoryInfo(entitiesPath) : Directory.CreateDirectory(entitiesPath);

        var schemaFolder = Directory.Exists(Path.Combine(entitiesFolder.FullName, schemaName)) ?
            new DirectoryInfo(Path.Combine(entitiesFolder.FullName, schemaName)) : entitiesFolder.CreateSubdirectory(schemaName);

        var TablesFolder = Directory.Exists(Path.Combine(schemaFolder.FullName, TABLES)) ?
            new DirectoryInfo(Path.Combine(schemaFolder.FullName, TABLES)) : schemaFolder.CreateSubdirectory(TABLES);

        foreach (var table in tables)
            File.WriteAllText(Path.Combine(TablesFolder.FullName, $"{table.Name}.cs"), GenerateTableFile(table, schemaName));

        var ViewsFolder = Directory.Exists(Path.Combine(schemaFolder.FullName, VIEWS)) ?
            new DirectoryInfo(Path.Combine(schemaFolder.FullName, VIEWS)) : schemaFolder.CreateSubdirectory(VIEWS);

        foreach (var view in views)
            File.WriteAllText(Path.Combine(ViewsFolder.FullName, $"{view.Key}.cs"), GenerateViewFile(view.Key, view.Value, schemaName));
    }

    private string GenerateViewFile(string name, string script, string schemaName) =>
@$"namespace SQLBridge.{schemaName};

public class {name} 
{{

    #region Original script
    /*
    {script}
    */
    #endregion    
}}";

    private string GenerateTableFile(TableEntity table, string schemaName) =>
@$"namespace SQLBridge.{schemaName};

public class {table.Name} 
{{
{GenerateTableProperties(table.Properties)}
                    
    #region Indexes
    /*                    
    {string.Join("\r\n\r\n", table.Indexes)}
    */
    #endregion

    #region Triggers    
    /*
    {string.Join("\r\n\r\n", table.Triggers)}
    */
    #endregion

    #region Original script
    /*
    {table.Script}
    */
    #endregion    
}}";

    private string GenerateTableProperties(IEnumerable<TableProperty> properties)
        => string.Join("\r\n",
            properties.Select(p =>
                (!string.IsNullOrEmpty(p.Comment) ? $"\t/// {p.Comment}\r\n" : "") +
                $"\tpublic {SQLToNetDataTypes[p.Type]} {p.Name} {{ get; set; }}" +
                (p.Length != null ? $" // Max length: {p.Length}" : "")));

    private (IEnumerable<TableEntity>, Dictionary<string, string>) ExtractInformation(string sqlScript)
    {
        var tableScripts = ExtractBlockScripts(ref sqlScript, TABLE);

        var viewScripts = ExtractBlockScripts(ref sqlScript, VIEW);

        var indexScripts = ExtractBlockScripts(ref sqlScript, INDEX);

        var triggerScripts = sqlScript.Contains($"{BLOCKPREFIX} {TRIGGER}") ?
            ExtractBlockScripts(ref sqlScript, TRIGGER) : new Dictionary<string, string>();

        var tables = tableScripts.Select(table =>
            ExtractTableInformation(
                table.Key,
                table.Value,
                indexScripts.Where(i => i.Key == table.Key).Select(i => i.Value).ToList(),
                triggerScripts.Where(t => t.Key == table.Key).Select(t => t.Value).ToList())
            ).ToList();

        return (tables,
                viewScripts.ToDictionary(k => k.Key, v => v.Value));
    }

    private IEnumerable<KeyValuePair<string, string>> ExtractBlockScripts(ref string sqlScript, string blockType)
    {
        var rawBlock = Regex.Split(sqlScript, @$"{BLOCKPREFIX} {blockType}\s*")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        sqlScript = rawBlock.Last();

        rawBlock = rawBlock.Skip(1).Take(rawBlock.Count() - 2).ToList();

        var lastBlock = Regex.Split(sqlScript, @$"{BLOCKPREFIX} \s*")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        rawBlock.Add(lastBlock.First());

        return rawBlock.Select(b => new KeyValuePair<string, string>(
            GetBlockName(b, blockType).ToUpper().Replace("\"", "").Replace($"{SchemaName.ToUpper()}.", ""),
            GetBlockCleaned(b).Trim())
        ).ToList();
    }
    private string GetBlockName(string block, string blockType)
    => blockType switch
    {
        INDEX => block.Substring(block.IndexOf(" ON ") + 4, block.IndexOf("(") - block.IndexOf(" ON ") - 5),
        TRIGGER => GetTriggerName(block),
        _ => block.Replace("$", "_").Split("\r\n")[0].Trim()
    };
    private string GetTriggerName(string block)
    {
        string pattern = @$"\b(\w+)\b(?=\s+(?:{TRIGGERPATTERN}))";
        var matches = Regex.Match(block.Replace("\"", ""), pattern);
        return matches.Groups[0].Value;
    }

    private string GetBlockCleaned(string block)
    => block.Substring(block.IndexOf($"{CREATE} "), block.LastIndexOf(";") - block.IndexOf($"{CREATE} ")) + ";";

    private TableEntity ExtractTableInformation(string tableName, string sqlScript, IEnumerable<string> indexScripts, IEnumerable<string> triggerScripts)
    {
        Dictionary<string, Tuple<string, string>> properties = ExtractTableProperties(tableName, sqlScript);
        Dictionary<string, string> comments = ExtractTableComments(sqlScript);

        var tableProps = new List<TableProperty>();

        foreach (KeyValuePair<string, Tuple<string, string>> property in properties)
        {
            var comment = comments.ContainsKey(property.Key) ? comments[property.Key] : null;

            tableProps.Add(new TableProperty(property.Key, property.Value.Item1, property.Value.Item2, comment));
        }

        return new TableEntity(tableName, tableProps, indexScripts, triggerScripts, sqlScript);
    }

    private static Dictionary<string, Tuple<string, string>> ExtractTableProperties(string tableName, string sqlScript)
    {
        sqlScript = sqlScript.Substring(sqlScript.IndexOf("(") + 1, sqlScript.LastIndexOf(")") - sqlScript.IndexOf("(") - 1);

        Dictionary<string, Tuple<string, string>> properties = new Dictionary<string, Tuple<string, string>>();

        foreach (var line in sqlScript.Split("\n"))
        {
            if (line.Trim() == string.Empty || line.Trim().StartsWith(")")) break;

            string propertyName = line.Split(" ")[0].Replace("\"", "").Trim();
            var dataType = line.Replace(line.Split(" ")[0], "");
            string propertyType = dataType.Trim();
            string propertyLength = "";
            if (dataType.Contains("("))
            {
                propertyType = dataType.Substring(0, dataType.IndexOf("(")).Trim();
                propertyLength = dataType.Substring(dataType.IndexOf("(") + 1, dataType.IndexOf(")") - dataType.IndexOf("(") - 1)
                    .Replace($" {BYTE}", "");
            }
            else propertyType = propertyType.Replace(",", "");

            if ((propertyType.StartsWith(DATE) || propertyType.StartsWith(NUMBER)) && propertyType.Contains(" "))
                propertyType = propertyType.Substring(0, propertyType.IndexOf(" ")).Trim();

            if (propertyName == tableName)
                propertyName = $"{tableName}_{propertyName}";

            properties[propertyName] = Tuple.Create(propertyType.Replace("\"", ""), propertyLength);
        }

        return properties;
    }

    private static Dictionary<string, string> ExtractTableComments(string sqlScript)
    {
        Dictionary<string, string> comments = new Dictionary<string, string>();
        string pattern = @$"{COLCOMMENT} (.*?)\.(.*?)\.(.*?) {IS} '(.*?)'";

        MatchCollection matches = Regex.Matches(sqlScript.Replace("\"", ""), pattern);
        foreach (Match match in matches)
        {
            string name = match.Groups[3].Value;
            string comment = match.Groups[4].Value;
            comments[name] = comment.Replace("'", "");
        }
        return comments;
    }
}
