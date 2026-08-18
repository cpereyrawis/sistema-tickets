namespace Asistente.Domain.Entities;

/// <summary>
/// Registro de un inicio de sesión.
///
/// Es una bitácora de acceso, no el mecanismo de sesión: la sesión viva la sostiene la
/// cookie de ASP.NET Core. Sirve para responder "quién entró, desde dónde y cuándo", que
/// es lo que pide NFR-007, y para detectar accesos raros.
///
/// Guarda lo mínimo: no se registran cabeceras completas ni datos que no hagan falta
/// (NFR-008, NFR-011).
/// </summary>
public sealed class SesionUsuario
{
    private SesionUsuario() { }

    public SesionUsuario(long userId, DateTime inicioUtc, string? direccionIp, string? agente)
    {
        UserId = userId;
        InicioUtc = inicioUtc;
        DireccionIp = direccionIp;
        Agente = Recortar(agente, 200);
    }

    public long Id { get; private set; }
    public long UserId { get; private set; }

    public DateTime InicioUtc { get; private set; }
    public DateTime? FinUtc { get; private set; }

    public string? DireccionIp { get; private set; }
    public string? Agente { get; private set; }

    /// <summary>Cómo terminó: cierre explícito, expiración, o null si sigue abierta.</summary>
    public string? MotivoCierre { get; private set; }

    public void Cerrar(DateTime ahoraUtc, string motivo)
    {
        FinUtc = ahoraUtc;
        MotivoCierre = motivo;
    }

    private static string? Recortar(string? valor, int largo) =>
        valor is null || valor.Length <= largo ? valor : valor[..largo];
}
