namespace Acelerador.Core.Dominio;

/// <summary>
/// Participación de una sociedad en un periodo, en el modelo de propiedad (RF-31, C02): % control, % consolidación,
/// % propiedad y método (RN-13). Un cambio aplica desde el periodo en que se registra (RN-14).
/// </summary>
public sealed record Participacion(string Sociedad, decimal Control, decimal Consolidacion, decimal Propiedad, MetodoConsolidacion Metodo);
