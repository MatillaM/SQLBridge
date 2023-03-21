namespace SQLBridge.OracleToDotNet.Classes;

internal class PkgFile
{
    internal string Name { get; set; }

    internal IEnumerable<PkgFunc> Funcs { get; set; }

    internal PkgFile(string name, IEnumerable<PkgFunc> funcs)
    {
        Name = name;
        Funcs = funcs;
    }
}
