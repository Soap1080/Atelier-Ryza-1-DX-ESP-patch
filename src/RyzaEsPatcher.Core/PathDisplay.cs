namespace RyzaEsPatcher.Core;

/// <summary>
/// Presenta rutas como las escribiría Windows. Hace falta porque Steam guarda su ruta en el
/// registro en minúsculas y con barras de Unix (<c>c:/program files (x86)/steam</c>).
/// </summary>
public static class PathDisplay
{
    public static string RealCase(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root)) return full;

            var result = root.ToUpperInvariant();
            var segments = full[root.Length..]
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                // Windows no distingue mayúsculas al buscar, pero devuelve el nombre tal cual
                // está en disco, que es justo lo que queremos mostrar.
                var match = Directory.Exists(result)
                    ? Directory.EnumerateFileSystemEntries(result, segment).FirstOrDefault()
                    : null;

                result = match ?? Path.Combine(result, segment);
            }

            return result;
        }
        catch (ArgumentException) { return path; }
        catch (IOException) { return path; }
        catch (UnauthorizedAccessException) { return path; }
        catch (NotSupportedException) { return path; }
    }
}
