using System.Text.RegularExpressions;

namespace Server.Tools;

public partial class DirectoryTool
{
    public static void RecursiveTraversal(string path, Action<FileInfo> action)
    {
        try
        {
            // 1. 处理当前目录的文件
            var files = Directory.GetFiles(path);
            var fileInfos = files.Select(file => new FileInfo(file));
            foreach (var fileInfo in fileInfos)
            {
                action(fileInfo);
            }

            // 2. 递归子目录
            var directories = Directory.GetDirectories(path);
            foreach (var dir in directories)
            {
                RecursiveTraversal(dir, action); // 自身递归调用
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Console.WriteLine($"无权限访问: {path}");
        }
        catch (DirectoryNotFoundException)
        {
            // Console.WriteLine($"未找到目录: {path}");
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

        // 使用正则表达式将字符串分割为数字和非数字序列
        // \d+ 匹配连续数字
        var xParts = IntegerRegex().Split(x).Where(p => !string.IsNullOrEmpty(p)).ToArray();
        var yParts = IntegerRegex().Split(y).Where(p => !string.IsNullOrEmpty(p)).ToArray();

        var length = Math.Min(xParts.Length, yParts.Length);

        for (var i = 0; i < length; i++)
        {
            if (xParts[i] == yParts[i])
                continue;

            // 检查当前片段是否为数字
            var isXDigit = char.IsDigit(xParts[i][0]);
            var isYDigit = char.IsDigit(yParts[i][0]);

            if (isXDigit && isYDigit)
            {
                // 1. 核心逻辑：按数值比较
                // 使用 BigInteger 或字符串去前导零比较，防止长数字溢出
                var xNormalized = xParts[i].TrimStart('0');
                var yNormalized = yParts[i].TrimStart('0');

                if (xNormalized.Length != yNormalized.Length)
                    return xNormalized.Length.CompareTo(yNormalized.Length);

                var partRes = string.Compare(xNormalized, yNormalized, StringComparison.Ordinal);
                return partRes != 0
                    ? partRes
                    :
                    // 2. 核心逻辑：数值相同时，前导零较少的（字符串长度较短的）排前面
                    xParts[i].Length.CompareTo(yParts[i].Length);
            }

            // 3. 非数字部分：使用不区分大小写的比较（模拟 StrCmpLogicalW 行为）
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