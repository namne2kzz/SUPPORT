namespace SUPPORT.Domain.Common;

/// <summary>Base class for all domain entities: time-ordered identity + creation timestamp.</summary>
public abstract class Entity
{
    /// <summary>Unique identifier (UUIDv7, so ids sort by creation time).</summary>
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    /// <summary>UTC creation time.</summary>
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
}
