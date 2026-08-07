namespace Versecue.Domain.ValueObjects;

/// <summary>
/// Value object representing an audio input device.
/// </summary>
public readonly record struct AudioDevice
{
    public string DeviceId { get; init; }
    public string Name { get; init; }

    public AudioDevice(string deviceId, string name)
    {
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public override string ToString() => Name;
}