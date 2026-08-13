using System.IO.Compression;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Services;

/// <summary>
/// Holds an opened ZIP archive plus a case-insensitive filename index built from it.
/// Disposing this disposes the underlying archive (and, if the archive failed to open, is a no-op).
/// </summary>
public sealed class ZipImageIndex : IDisposable
{
    private readonly ZipArchive? _archive;
    public IReadOnlyDictionary<string, ZipArchiveEntry> EntriesByName { get; }

    /// <summary>False when the ZIP failed to open at all (corrupt/not a ZIP) — as opposed to opening fine but being empty.</summary>
    public bool IsValidArchive => _archive is not null;

    public ZipImageIndex(ZipArchive? archive, Dictionary<string, ZipArchiveEntry> entriesByName)
    {
        _archive = archive;
        EntriesByName = entriesByName;
    }

    public bool TryGet(string fileName, out ZipArchiveEntry entry) =>
        EntriesByName.TryGetValue(fileName.Trim(), out entry!);

    public void Dispose() => _archive?.Dispose();
}

public class ZipImageService : IZipImageService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public ZipImageIndex OpenAndIndex(Stream zipStream, List<string> errors)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            errors.Add("The uploaded images archive is not a valid ZIP file.");
            return new ZipImageIndex(null, new());
        }

        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            // Directory entries have an empty Name (FullName ends with '/'); skip them.
            if (string.IsNullOrEmpty(entry.Name)) continue;

            var name = entry.Name.Trim();
            var ext = Path.GetExtension(name);
            if (!AllowedExtensions.Contains(ext))
            {
                errors.Add($"Image '{name}' in the ZIP has an unsupported extension. Allowed: .jpg, .jpeg, .png, .webp");
                continue;
            }

            if (!entries.TryAdd(name, entry))
                errors.Add($"Duplicate filename '{name}' found inside the ZIP archive.");
        }

        return new ZipImageIndex(archive, entries);
    }
}
