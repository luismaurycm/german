using Acelerador.Web;

var app = AplicacionAcelerador.Construir(args);
await AplicacionAcelerador.InicializarAsync(app);
app.Run();

/// <summary>Punto de entrada; público para las pruebas de integración.</summary>
public partial class Program;
