namespace GMPPro20DataUpload.Models;

/// <summary>
/// Holds values shared between collections during a processing run.
/// Collections publish values here; later collections consume them.
/// Key is the FlowKey defined in the Schema Excel.
/// </summary>
public class FlowContext
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stores a value under the given flow key (publish).</summary>
    public void Publish(string key, string? value) => _values[key] = value;

    /// <summary>Retrieves a value by flow key (consume). Returns null if not found.</summary>
    public string? Consume(string key) => _values.TryGetValue(key, out var value) ? value : null;

    /// <summary>Returns true if the given flow key has been published.</summary>
    public bool Contains(string key) => _values.ContainsKey(key);

    /// <summary>Clears all published values.</summary>
    public void Clear() => _values.Clear();
}
