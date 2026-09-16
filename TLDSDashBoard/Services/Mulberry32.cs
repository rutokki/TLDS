namespace TLDSDashBoard.Services;

/// <summary>
/// Deterministic seeded PRNG — a direct C# port of the mulberry32 algorithm used by the
/// original HTML prototype, so the same seed reproduces series with the same shape/character.
/// </summary>
public sealed class Mulberry32
{
    private uint _t;

    public Mulberry32(uint seed)
    {
        _t = seed;
    }

    /// <summary>Returns the next pseudo-random value in [0, 1).</summary>
    public double NextDouble()
    {
        unchecked
        {
            _t += 0x6D2B79F5u;
            uint t = _t;
            uint r = (t ^ (t >> 15)) * (1u | t);
            r = (r + (r ^ (r >> 7)) * (61u | r)) ^ r;
            return (r ^ (r >> 14)) / 4294967296.0;
        }
    }
}
