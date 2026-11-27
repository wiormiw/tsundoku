namespace Tsundoku.Engine;

public sealed record Activity(Guid Id, string Name, Unit Unit, int Floor, int Stretch, Cadence Cadence, string? Deliverable = null);
