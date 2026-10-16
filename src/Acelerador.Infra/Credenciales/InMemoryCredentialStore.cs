using System.Collections.Concurrent;
using Acelerador.Core.Puertos;
using Acelerador.Sap;

namespace Acelerador.Infra.Credenciales;

/// <summary>
/// Almacén en memoria para pruebas y para desarrollo fuera de Windows. No escribe nada en disco:
/// las credenciales se pierden al cerrar la aplicación. No se usa en el producto instalado (Windows).
/// </summary>
public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly ConcurrentDictionary<string, CredencialSap> _valores = new(StringComparer.Ordinal);

    public bool EsPersistente => false;

    public int Cantidad => _valores.Count;

    public void Guardar(string referencia, string usuario, CredencialSap credencial) => _valores[referencia] = credencial;

    public CredencialSap? Leer(string referencia) => _valores.GetValueOrDefault(referencia);

    public void Eliminar(string referencia) => _valores.TryRemove(referencia, out _);
}
