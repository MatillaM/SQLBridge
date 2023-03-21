using System.Text;
using System.Text.RegularExpressions;
using SQLBridge.OracleToDotNet.Classes;

namespace SQLBridge.OracleToDotNet;

internal class ProcessPackages
{
    const string PROCEDURE = nameof(PROCEDURE);
    const string FUNCTION = nameof(FUNCTION);
    string SchemaName = nameof(SchemaName);

    internal ProcessPackages(string schemaPackagesFolderPath, string codePath, string schemaName)
    {
        SchemaName = schemaName;

        var PkgFiles = new List<PkgFile>();
        var ExternalCalls = new List<string>();
               
        codePath = Path.Combine(codePath, schemaName);
        var codeFolder = Directory.Exists(codePath) ? new DirectoryInfo(codePath) : Directory.CreateDirectory(codePath);

        string[] pkgFiles = Directory.GetFiles(schemaPackagesFolderPath, "*.*", SearchOption.AllDirectories);
        foreach (string file in pkgFiles)
        {
            var fileInfo = new FileInfo(file);
            var packageName = fileInfo.Name.Replace($"{fileInfo.Extension}", "").Trim().ToUpper();

            var ProceduresAndFunctions = ExtractProceduresAndFunctions(file);

            var funcs = new List<PkgFunc>();
            foreach (KeyValuePair<string, string> func in ProceduresAndFunctions)
            {
                var Calls = ExtractCalls(func.Value);
                var (tablesInSelect, tablesInInsert, tablesInUpdate, tablesInDelete) = ExtractTablesAndViews(func.Value);

                var pkgFunc = new PkgFunc(func.Key, func.Value, tablesInSelect, tablesInInsert, tablesInUpdate, tablesInDelete, Calls);
                ExternalCalls.Add($"{schemaName}.{packageName}.{func.Key}");

                funcs.Add(pkgFunc);
            }
            var pkgFile = new PkgFile(packageName, funcs);
            PkgFiles.Add(pkgFile);

            SetInternalCalls(pkgFile);
        }

        PkgFiles.ForEach(pkgFile => SetExternalCalls(pkgFile, ExternalCalls));

        foreach (var pkgFile in PkgFiles)
        {
            var pkgFolder = Directory.Exists(Path.Combine(codeFolder.FullName, pkgFile.Name)) ?
                new DirectoryInfo(Path.Combine(codeFolder.FullName, pkgFile.Name)) : codeFolder.CreateSubdirectory(pkgFile.Name);

            foreach (var pkgFunc in pkgFile.Funcs)
                File.WriteAllText(Path.Combine(pkgFolder.FullName, $"{pkgFunc.Name}.cs"), GeneratePackageFile(pkgFunc, schemaName, pkgFile.Name));
        }
    }

    private void SetExternalCalls(PkgFile pkgFile, List<string> externalCalls)
    {
        foreach (var func in pkgFile.Funcs)
        {
            if (!func.Calls.Any())
                return;

            func.ExternalCalls = externalCalls.Intersect(func.Calls, StringComparer.InvariantCultureIgnoreCase);
        }
    }

    private void SetInternalCalls(PkgFile pkgFile)
    {
        var internalFuncs = pkgFile.Funcs.Select(f => f.Name);

        foreach (var func in pkgFile.Funcs)
        {
            if (!func.Calls.Any())
                return;

            func.InternalCalls = func.Calls.Intersect(internalFuncs, StringComparer.InvariantCultureIgnoreCase);
        }
    }

    private string GeneratePackageFile(PkgFunc func, string schema, string packageName) =>
@$"namespace SQLBridge.{schema};
public static partial class {packageName} 
{{
    {GenerateFuncContent(func)}
}}";
    private string GenerateFuncContent(PkgFunc func) =>
    $@"public static void {func.Name}()
    {{
{string.Join("\r\n", func.InternalCalls.Select(i => $"\t\t{i}();"))}

{string.Join("\r\n", func.ExternalCalls.Select(i => $"\t\t{i}();"))}

        #region Selects to tables:
{string.Join("\r\n", func.TablesInSelects.Select(i => $"\t\tnew {i}();").Distinct())}
        #endregion

        #region Inserts in tables:
{string.Join("\r\n", func.TablesInInserts.Select(i => $"\t\tnew {i}();").Distinct())}
        #endregion

        #region Updates in tables:
{string.Join("\r\n", func.TablesInUpdates.Select(i => $"\t\tnew {i}();").Distinct())}
        #endregion

        #region Deletes in tables:
{string.Join("\r\n", func.TablesInDeletes.Select(i => $"\t\tnew {i}();").Distinct())}
        #endregion

        #region Original code
        /*
        {func.Script}
        */
        #endregion
    }}";

    private Dictionary<string, string> ExtractProceduresAndFunctions(string filePath)
    {
        Dictionary<string, string> proceduresAndFunctions = new Dictionary<string, string>();

        string fileContent = Utils.RemoveComments(File.ReadAllText(filePath, Encoding.Latin1));

        var lines = fileContent.Split("\n", StringSplitOptions.RemoveEmptyEntries);
        var lineIndex = Enumerable.Range(0, lines.Count());
        var lineIndexes = lines.Zip(lineIndex, (l, i) => { return l.Contains($"{PROCEDURE} ") || l.Contains($"{FUNCTION} ") ? i : -1; })
            .Where(i => i >= 0);

        if (!lineIndexes.Any()) return proceduresAndFunctions;

        var prev = lineIndexes.First();

        foreach (var next in lineIndexes.Skip(1))
        {
            string procedureOrFunction = string.Join("\n", lines.Zip(lineIndex, (l, i) => { return i >= prev && i < next ? l : string.Empty; }).Where(l => l != string.Empty));

            if (!procedureOrFunction.Contains("BEGIN")) { prev = next; continue; }
            string name = ProcedureOrFunctionGetName(lines[prev]);

            if (proceduresAndFunctions.ContainsKey(name))
                name += procedureOrFunction.Trim().ToUpper().StartsWith("FUNCTION") ? "_func" : "_proc";

            proceduresAndFunctions.Add(name, procedureOrFunction.Trim());
            prev = next;
        }
        string lastProcedureOrFunction = string.Join("\n", lines.Zip(lineIndex, (l, i) => { return i >= prev ? l : string.Empty; }).Where(l => l != string.Empty));

        if (lastProcedureOrFunction.Contains("BEGIN"))
        {
            string name = ProcedureOrFunctionGetName(lines[prev]);
            proceduresAndFunctions.Add(name, lastProcedureOrFunction.Trim());
        }
        return proceduresAndFunctions;
    }
    private string ProcedureOrFunctionGetName(string text)
    {
        var name = text.IndexOf("(") > 0 ? text.Substring(0, text.IndexOf("(")) : text;

        if (name.Contains(PROCEDURE)) name = name.Replace(PROCEDURE, "").Replace(";", "").Trim();
        else name = name.Replace(FUNCTION, "").Trim();

        if (name.Contains(" ")) name = name.Split(" ")[0];

        return name.ToLower();
    }

    private List<string> ExtractCalls(string content)
    {
        content = content.Substring(content.IndexOf("BEGIN"));

        string extractArgsRegex = @"\b(\w+[\w\.]*(\w+))\s*\(\s*";
        MatchCollection matches = Regex.Matches(content, extractArgsRegex);

        List<string> callsList = new List<string>();
        foreach (Match match in matches)
        {
            string call = match.Value;
            if (call.ToUpper().Trim().StartsWith("RETURN")) continue;
            if (!IsLocalOrInAllowedSchemas(call)) continue;
            callsList.Add(GetCallName(call));
        }

        return callsList;
    }
    private string GetCallName(string text)
    {
        var name = text.IndexOf("(") > 0 ? text.Substring(0, text.IndexOf("(")) : text;
        return name.Replace("$", "_").ToLower().Trim();
    }
    private (IEnumerable<string>, IEnumerable<string>, IEnumerable<string>, IEnumerable<string>) ExtractTablesAndViews(string content)
    {
        string selectPattern = @"(?i)SELECT\s+.*\s+FROM\s+(\w+\.\w+)";
        string insertPattern = @"(?i)INSERT\s+INTO\s+(\w+(?:\.\w+)?)\s";
        string updatePattern = @"(?i)UPDATE\s+(\w+(?:\.\w+)?)\s+";
        string deletePattern = @"(?i)DELETE\s+FROM\s+(\w+(?:\.\w+)?)";

        MatchCollection selectMatches = Regex.Matches(content, selectPattern);
        MatchCollection insertMatches = Regex.Matches(content, insertPattern);
        MatchCollection updateMatches = Regex.Matches(content, updatePattern);
        MatchCollection deleteMatches = Regex.Matches(content, deletePattern);

        return (selectMatches.Where(m => IsInAllowedSchemas(m.Groups[1].Value)).Select(m => GetTableName(m.Groups[1].Value)),
                insertMatches.Where(m => IsInAllowedSchemas(m.Groups[1].Value)).Select(m => GetTableName(m.Groups[1].Value)),
                updateMatches.Where(m => IsInAllowedSchemas(m.Groups[1].Value)).Select(m => GetTableName(m.Groups[1].Value)),
                deleteMatches.Where(m => IsInAllowedSchemas(m.Groups[1].Value)).Select(m => GetTableName(m.Groups[1].Value)));
    }
    private string GetTableName(string tableName)
    => tableName.ToLower().Replace("FROM ", "").Trim().ToUpper();

    private bool IsLocalOrInAllowedSchemas(string name)
        => !name.Contains(".") || name.ToUpper().StartsWith("PK_") || name.ToUpper().Contains($"{SchemaName}.");

    private bool IsInAllowedSchemas(string name)
    => name.Contains(".") && name.ToUpper().Contains($"{SchemaName}.");
}
