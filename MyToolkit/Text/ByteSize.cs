namespace MyToolkit.Text;

/// <summary>A byte count as people read it. Pure, so it is unit-tested.</summary>
public static class ByteSize
{
    private const double Megabyte = 1024 * 1024;
    private const double Gigabyte = 1024 * Megabyte;

    /// <summary>"512 MB" under a gigabyte, "1.5 GB" from there; one decimal at most, in the current culture.</summary>
    public static string Format(long bytes) => bytes >= Gigabyte ? $"{bytes / Gigabyte:0.#} GB" : $"{bytes / Megabyte:0.#} MB";
}
