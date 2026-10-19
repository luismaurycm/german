using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Servicios;

/// <summary>Lectura de BPC que comparten C01 a C04, C02 y los asientos: el resultado o el mensaje de error ya saneado.</summary>
public static class LecturaBpc
{
    /// <summary>Origen que ningún registro tiene: la consulta trae solo validaciones, asientos y estados de trabajo.</summary>
    public const string SinRegistros = "-";

    public static async Task<(ResultadoConsultaBpc? Datos, string? Error)> LeerAsync(AccesoSap acceso, ISapGateway sap, Guid clienteId, ConsultaBpc consulta, CancellationToken ct)
    {
        var destino = await acceso.DestinoAsync(clienteId, SistemaSap.Bpc, ct);
        if (destino.Destino is null)
        {
            return (null, destino.Error);
        }

        try
        {
            var r = await sap.ConsultarBpcAsync(destino.Destino, consulta, ct);
            return r.Exitosa ? (r, null) : (null, SanitizadorMensajes.Limpiar(r.Mensaje, destino.Credencial));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, SanitizadorMensajes.Limpiar($"Error inesperado al llamar a SAP: {ex.Message}", destino.Credencial));
        }
    }

    /// <summary>Sociedades bloqueadas en el periodo (RN-17); vacío si BPC no responde (BPC hace cumplir el bloqueo igual).</summary>
    public static async Task<IReadOnlySet<string>> BloqueadasAsync(AccesoSap acceso, ISapGateway sap, Guid clienteId, string periodo, CancellationToken ct)
    {
        var (datos, _) = await LeerAsync(acceso, sap, clienteId, new ConsultaBpc(Consolidacion.MotorConsolidacion.Modelo, Consolidacion.MotorConsolidacion.PeriodoBpc(periodo), Origen: SinRegistros), ct);
        return ReglasCierreMensual.Bloqueadas(datos?.Bloqueos ?? []);
    }
}
