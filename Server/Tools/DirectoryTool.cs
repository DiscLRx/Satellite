using System.Text.RegularExpressions;

namespace Server.Tools;

public partial class DirectoryTool
{
    public static void RecursiveTraversal(string path, Action<FileInfo> action, bool sort = true)
    {
        try
        {
            var files = Directory.GetFiles(path);
            if (sort)
            {
                Array.Sort(
                    files,
                    (left, right) =>
                        PathCompare(
                            Path.GetFileName(left),
                            Path.GetFileName(right)
                        )
                );
            }

            foreach (var file in files)
            {
                action(new FileInfo(file));
            }
            var directories = Directory.GetDirectories(path);
            if (sort)
            {
                Array.Sort(
                    directories,
                    (left, right) =>
                        PathCompare(
                            Path.GetFileName(left),
                            Path.GetFileName(right)
                        )
                );
            }

            foreach (var dir in directories)
            {
                RecursiveTraversal(dir, action, sort);
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    public static string FormatBytes(long size)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = size;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{size} {units[unitIndex]}"
            : $"{value:0.##} {units[unitIndex]}";
    }


    [GeneratedRegex(@"(\d+)")]
    private static partial Regex IntegerRegex();
    public static int PathCompare(string x, string y)
    {
        if (x == y)
            return 0;
        var xParts = IntegerRegex().Split(x).Where(p => !string.IsNullOrEmpty(p)).ToArray();
        var yParts = IntegerRegex().Split(y).Where(p => !string.IsNullOrEmpty(p)).ToArray();

        var length = Math.Min(xParts.Length, yParts.Length);

        for (var i = 0; i < length; i++)
        {
            if (xParts[i] == yParts[i])
                continue;
            var isXDigit = char.IsDigit(xParts[i][0]);
            var isYDigit = char.IsDigit(yParts[i][0]);

            if (isXDigit && isYDigit)
            {
                var xNormalized = xParts[i].TrimStart('0');
                var yNormalized = yParts[i].TrimStart('0');

                if (xNormalized.Length != yNormalized.Length)
                    return xNormalized.Length.CompareTo(yNormalized.Length);

                var partRes = string.Compare(xNormalized, yNormalized, StringComparison.Ordinal);
                return partRes != 0
                    ? partRes
                    :
                    xParts[i].Length.CompareTo(yParts[i].Length);
            }
            var result = string.Compare(
                xParts[i],
                yParts[i],
                StringComparison.CurrentCultureIgnoreCase
            );
            if (result != 0)
                return result;
        }

        return xParts.Length.CompareTo(yParts.Length);
    }

}