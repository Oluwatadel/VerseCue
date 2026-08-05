namespace Versecue.Domain.ValueObjects;

/// <summary>
/// Value object representing a display/monitor device.
/// </summary>
public readonly record struct Display
{
    public string DeviceId { get; init; }
    public string Name { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool IsPrimary { get; init; }

    public Display(string deviceId, string name, int width, int height, bool isPrimary)
    {
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Width = width;
        Height = height;
        IsPrimary = isPrimary;
    }

    public override string ToString() => $"{Name} ({Width}x{Height}){(IsPrimary ? " [Primary]" : "")}";
}