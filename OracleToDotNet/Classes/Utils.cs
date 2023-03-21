using System.Text.RegularExpressions;

namespace SQLBridge.OracleToDotNet.Classes;

internal static class Utils
{
    internal static string RemoveComments(string text)
    {
        Regex regex = new Regex("(?s)/\\*.*?\\*.*?(?=\r|\n)");
        return regex.Replace(text, "").Replace("*/", "").Replace("ñ", "n").Replace("Ñ", "N");
    }

    internal static string RemoveEmptyLines(string text)
    => text.Replace("\r\n\n", "\r\n").Replace("\n\n", "\n").Replace("\r\n\r\n", "\r\n").Replace("\r\n\t\r\n", "\r\n");           
}
