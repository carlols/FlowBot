using System.Text.RegularExpressions;

namespace FlowBot;

public static partial class EmojiImportName
{
    public static string Normalize(string value) =>
        value.Trim();

    public static bool IsValid(string name) =>
        ValidEmojiNameRegex().IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9_]{2,32}$")]
    private static partial Regex ValidEmojiNameRegex();
}
