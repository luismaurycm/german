using Acelerador.Core.Puertos;

namespace Acelerador.Infra;

/// <summary>El usuario de la sesión de Windows (ADR-05): DOMINIO\usuario. Fuera de Windows, el usuario del sistema.</summary>
public sealed class UsuarioSistemaOperativo : IUsuarioActual
{
    public string Nombre { get; } = OperatingSystem.IsWindows()
        ? $@"{Environment.UserDomainName}\{Environment.UserName}"
        : Environment.UserName;
}
