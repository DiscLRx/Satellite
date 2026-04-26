using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SatelliteUI.Tools;

public class CustomBackgroundLoader
{
    private const string BackgroundDirectory = "Custom/Background";
    private static DateTime? _bgDirLastModified;
    private static IEnumerator<string>? _bgFileEnumerator;

    public static string? GetCustomBackground()
    {
        if (!Directory.Exists(BackgroundDirectory))
        {
            Directory.CreateDirectory(BackgroundDirectory);
            return null;
        }
        var lastBgFile = _bgFileEnumerator?.Current;
        var dirModifiedTime = Directory.GetLastWriteTime(BackgroundDirectory);
        _bgDirLastModified ??= dirModifiedTime;

        if (_bgFileEnumerator != null && _bgDirLastModified == dirModifiedTime)
            if (_bgFileEnumerator.MoveNext())
            {
                var bgFile = _bgFileEnumerator.Current;
                if (File.Exists(bgFile)) return bgFile;
            }

        var bgFiles = Directory.GetFiles(BackgroundDirectory);
        _bgDirLastModified = dirModifiedTime;
        switch (bgFiles.Length)
        {
            case 0:
                return null;
            case 1:
                return bgFiles[0];
        }

        Random.Shared.Shuffle(bgFiles);
        _bgFileEnumerator = bgFiles.ToList().GetEnumerator();
        _bgFileEnumerator.MoveNext();
        if (lastBgFile == _bgFileEnumerator.Current) _bgFileEnumerator.MoveNext();

        return _bgFileEnumerator.Current;
    }
}