using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ra2Converter.Formats;

public sealed class IniSection
{
    public string Name { get; }
    private readonly Dictionary<string, string> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<KeyValuePair<string, string>> _ordered = new();
    private int _autoKeyCounter;

    public IReadOnlyList<KeyValuePair<string, string>> OrderedEntries => _ordered;

    public IniSection(string name)
    {
        Name = name;
    }

    public void Set(string key, string value)
    {
        if (key == "+")
        {
            key = $"+_{_autoKeyCounter++}";
        }
        _entries[key] = value;
        _ordered.Add(new KeyValuePair<string, string>(key, value));
    }

    public string? GetString(string key, string? defaultValue = null)
    {
        return _entries.TryGetValue(key, out string? v) ? v : defaultValue;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        if (!_entries.TryGetValue(key, out string? v) || string.IsNullOrWhiteSpace(v))
            return defaultValue;
        string cleaned = v.Split(',')[0].Trim();
        return int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        if (!_entries.TryGetValue(key, out string? v) || string.IsNullOrWhiteSpace(v))
            return defaultValue;
        string cleaned = v.Trim().TrimEnd('%');
        if (float.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            return v.Trim().EndsWith('%') ? parsed * 0.01f : parsed;
        }
        return defaultValue;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        if (!_entries.TryGetValue(key, out string? v) || string.IsNullOrWhiteSpace(v))
            return defaultValue;
        string lower = v.Trim().ToLowerInvariant();
        if (lower is "yes" or "true" or "1" or "on") return true;
        if (lower is "no" or "false" or "0" or "off") return false;
        return defaultValue;
    }

    public List<string> GetCsvList(string key)
    {
        var list = new List<string>();
        if (!_entries.TryGetValue(key, out string? v) || string.IsNullOrWhiteSpace(v))
            return list;
        foreach (string part in v.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0) list.Add(trimmed);
        }
        return list;
    }
}

public sealed class IniDocument
{
    private readonly Dictionary<string, IniSection> _sections = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<IniSection> Sections => _sections.Values;

    public IniSection? GetSection(string name)
    {
        return _sections.TryGetValue(name, out var s) ? s : null;
    }

    public IniSection GetOrAddSection(string name)
    {
        if (!_sections.TryGetValue(name, out var s))
        {
            s = new IniSection(name);
            _sections[name] = s;
        }
        return s;
    }

    public static IniDocument Parse(byte[] bytes)
    {
        var doc = new IniDocument();
        doc.Merge(Encoding.ASCII.GetString(bytes));
        return doc;
    }

    public static IniDocument ParseText(string content)
    {
        var doc = new IniDocument();
        doc.Merge(content);
        return doc;
    }

    public void Merge(string content)
    {
        IniSection? current = null;
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            int commentIdx = line.IndexOf(';');
            if (commentIdx >= 0) line = line.Substring(0, commentIdx);
            line = line.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith('[') && line.EndsWith(']') && line.Length > 2)
            {
                string secName = line.Substring(1, line.Length - 2).Trim();
                current = GetOrAddSection(secName);
                continue;
            }

            if (current == null) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            string key = line.Substring(0, eq).Trim();
            string val = line.Substring(eq + 1).Trim();
            current.Set(key, val);
        }
    }
}
