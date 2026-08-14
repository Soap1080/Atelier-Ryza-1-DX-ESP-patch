namespace RyzaEsPatcher.Core;

public sealed class PatchEngineException : Exception
{
    public PatchEngineException(string message) : base(message) { }
    public PatchEngineException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Aplica un delta binario a un archivo de origen para producir el destino.</summary>
public interface IPatchEngine : IDisposable
{
    void Apply(string oldFile, string diffFile, string newFile, CancellationToken ct);
}
