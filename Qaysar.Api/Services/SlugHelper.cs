using System.Text.RegularExpressions;

namespace Qaysar.Api.Services;

public static class SlugHelper
{
    public static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Guid.NewGuid().ToString("N")[..8];
        var s = input.ToLowerInvariant().Trim();
        s = Regex.Replace(s, @"[^a-z0-9\u0600-\u06FF\s-]", "");
        s = Regex.Replace(s, @"\s+", "-");
        s = Regex.Replace(s, @"-+", "-");
        return s.Trim('-');
    }
}
