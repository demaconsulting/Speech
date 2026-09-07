namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Bundles the four numeric bounds required to construct a <see cref="NumericParameter"/>
///     (<see cref="Minimum"/>, <see cref="Maximum"/>, <see cref="Step"/>, and
///     <see cref="Default"/>) into a single value, keeping
///     <see cref="NumericParameter(string, string, string, NumericParameterBounds, string?, bool)"/>'s
///     parameter count small. <see cref="NumericParameter"/> still validates and exposes these
///     four values as its own individual properties; this type exists only to group the
///     constructor arguments.
/// </summary>
/// <param name="Minimum">The smallest value a host may supply for the parameter.</param>
/// <param name="Maximum">The largest value a host may supply for the parameter.</param>
/// <param name="Step">The smallest meaningful increment between adjacent values.</param>
/// <param name="Default">The value used when a host supplies none.</param>
public readonly record struct NumericParameterBounds(double Minimum, double Maximum, double Step, double Default);
