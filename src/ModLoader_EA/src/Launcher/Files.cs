namespace MelvorModLoaderLauncher;

internal static class Files
{
    /// <summary>
    /// Hard links share one file record, so a link to <paramref name="source"/> always matches it; a copy matches as long
    /// as the source is unchanged (File.Copy keeps the write time).
    /// </summary>
    public static bool AreSame(string source, string target)
    {
        var a = new FileInfo(source);
        var b = new FileInfo(target);
        return b.Exists && a.Length == b.Length && a.LastWriteTimeUtc == b.LastWriteTimeUtc;
    }

    /// <summary>Makes <paramref name="target"/> a hard link to <paramref name="source"/>, or a copy where links aren't supported.</summary>
    public static void Mirror(string source, string target)
    {
        if (AreSame(source, target))
        {
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Delete(target);
        if (!NativeMethods.CreateHardLink(target, source, 0))
        {
            // Other volume, or a file system without hard links (FAT/exFAT).
            File.Copy(source, target);
        }
    }

    public static void WriteIfChanged(string path, byte[] content)
    {
        if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(content))
        {
            File.WriteAllBytes(path, content);
        }
    }
}
