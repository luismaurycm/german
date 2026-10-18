namespace Acelerador.Tests.Soporte;

/// <summary>Prueba que solo corre en Windows (Administrador de credenciales); en otros sistemas queda omitida.</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Requiere Windows: Administrador de credenciales (DPAPI).";
        }
    }
}

/// <summary>Ruta de la raíz del repositorio, para las pruebas que revisan documentos y el paquete ABAP.</summary>
public static class Repositorio
{
    public static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Acelerador.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encontró Acelerador.sln.");
    }

    public static string Leer(params string[] partes) => File.ReadAllText(Path.Combine([Raiz(), .. partes]));
}
