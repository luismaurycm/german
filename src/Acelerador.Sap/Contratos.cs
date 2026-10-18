namespace Acelerador.Sap;

/// <summary>Sistemas SAP del cliente con los que habla el acelerador (RF-01).</summary>
public enum SistemaSap
{
    Erp,
    Bw,
    Bpc,
}

/// <summary>Parámetros de conexión RFC. No incluye la credencial: esa vive en el almacén de credenciales (ADR-04).</summary>
public sealed record ParametrosConexion(string Host, string Mandante, string UsuarioTecnico);

/// <summary>
/// Credencial del usuario técnico. Envuelve el secreto para que nunca salga por ToString,
/// interpolación o serialización accidental a un registro (RNF-Seg-02).
/// </summary>
public sealed class CredencialSap
{
    public const string Mascara = "••••••••";

    private readonly string _valor;

    public CredencialSap(string valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            throw new ArgumentException("La credencial no puede estar vacía.", nameof(valor));
        }

        _valor = valor;
    }

    /// <summary>Solo para el adaptador RFC y el sanitizador de mensajes.</summary>
    public string Revelar() => _valor;

    public override string ToString() => Mascara;
}

/// <summary>Destino RFC completo: sistema, parámetros y credencial.</summary>
public sealed record DestinoSap(SistemaSap Sistema, ParametrosConexion Parametros, CredencialSap Credencial);

/// <summary>Identificación del sistema que responde: SID y producto (p. ej. «SAP BW 7.5»).</summary>
public sealed record InfoSistemaSap(string Sid, string Producto);

public sealed record ResultadoPrueba(bool Exitosa, string Mensaje, InfoSistemaSap? Sistema = null);

public sealed record ResultadoValidacionErpBw(bool Exitosa, string Mensaje);

public enum ProductoBw
{
    NoReconocido,
    Bw75,
    Bw4Hana,
}

public enum ProductoBpc
{
    NoReconocido,
    Bpc101,
    Bpc2021,
}

public enum ModeloBpc
{
    Estandar,
    Embebido,
}

/// <summary>Versión de BW detectada. <see cref="Release"/> es el texto que devuelve SAP (p. ej. «7.5», «2.0», «7.4»).</summary>
public sealed record VersionBw(ProductoBw Producto, string Release);

public sealed record VersionBpc(ProductoBpc Producto, string Release, ModeloBpc Modelo);

/// <summary>Lo que el escáner lee de los tres sistemas para identificar la arquitectura (RF-04).</summary>
public sealed record PaisajeSap(string SidErp, string SidBw, string SidBpc, VersionBw Bw, VersionBpc Bpc);

public sealed record ResultadoDeteccion(bool Exitosa, string Mensaje, PaisajeSap? Paisaje = null);
