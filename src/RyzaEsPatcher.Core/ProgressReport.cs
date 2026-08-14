namespace RyzaEsPatcher.Core;

/// <summary>Avance de una operación larga: texto para el usuario y fracción de 0 a 1.</summary>
public sealed record ProgressReport(string Message, double Fraction);
