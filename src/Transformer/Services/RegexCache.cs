using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Transformer.Services;

internal static class RegexCache
{
    private static readonly ConcurrentDictionary<string, Regex> _cache = new(StringComparer.Ordinal);

    public static Regex Get(string pattern) =>
        _cache.GetOrAdd(pattern, p => new Regex(p, RegexOptions.Compiled));
}
