using Asistente.Common;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Tests;

/// <summary>
/// Verifica la máquina de estados contra los criterios de aceptación de §17.
/// Son pruebas puras: sin base de datos, sin web y con el reloj fijado a mano.
/// </summary>
public class WorkdayTests
{
    private static readonly DateTime T0 = new(2026, 8, 16, 9, 0, 0, DateTimeKind.Utc);
    private const long Usuario = 1L;

    private static TicketRef Ticket(string id) =>
        new(id, "CLI-001", "Molinos del Norte S.A.", $"Título de {id}");

    private static Workday JornadaActiva() =>
        Workday.Comenzar(Usuario, Ticket("SUP-1"), T0, DateOnly.FromDateTime(T0));

    // ---------- AC-01 / AC-02 ----------

    [Fact]
    public void SinJornada_LaUnicaAccionEsComenzarElDia()
    {
        var acciones = Workday.AccionesHabilitadas(EstadoJornada.Pendiente);
        Assert.Equal([TipoAccion.ComenzarDia], acciones);
    }

    [Fact]
    public void ComenzarDia_DejaUnaUnicaSesionAbierta()
    {
        var jornada = JornadaActiva();

        Assert.Equal(EstadoJornada.Activa, jornada.Estado);
        Assert.Single(jornada.Sesiones);
        Assert.Single(jornada.Sesiones, s => s.EstaAbierta);
        Assert.Equal("SUP-1", jornada.TicketPrincipal!.TicketId);
    }

    // ---------- AC-03 ----------

    [Fact]
    public void FinTarea_CierraLaAnteriorEIniciaLaSiguienteEnLaMismaMarca()
    {
        var jornada = JornadaActiva();
        var t = T0.AddMinutes(80);

        var resultado = jornada.FinTarea(Ticket("SUP-2"), t);

        Assert.True(resultado.Ok);
        var orden = jornada.Sesiones.OrderBy(s => s.InicioUtc).ToList();
        Assert.Equal(2, orden.Count);
        Assert.Equal(t, orden[0].FinUtc);
        Assert.Equal(t, orden[1].InicioUtc);
        Assert.Single(jornada.Sesiones, s => s.EstaAbierta);
        Assert.Equal("SUP-2", jornada.TicketPrincipal!.TicketId);
    }

    // ---------- AC-05 / AC-06 ----------

    [Fact]
    public void Interrupcion_GeneraCuatroEventosConCorrelacionComun()
    {
        var jornada = JornadaActiva();
        var inicio = T0.AddMinutes(30);

        var resultado = jornada.RegistrarInterrupcion(
            Ticket("SUP-9"), inicio, 20, T0.AddMinutes(60));

        Assert.True(resultado.Ok);

        var grupos = jornada.Eventos
            .GroupBy(e => e.CorrelationId)
            .Select(g => g.Select(e => e.Tipo).ToList())
            .ToList();

        var cuatro = Assert.Single(grupos, g => g.Count == 4);
        Assert.Equal(
            [TipoEvento.FinPrincipal, TipoEvento.InicioInterrupcion,
             TipoEvento.FinInterrupcion, TipoEvento.InicioPrincipal],
            cuatro);
    }

    [Fact]
    public void Interrupcion_TerminaEnInicioMasDuracionYReanudaElMismoTicket()
    {
        var jornada = JornadaActiva();
        var inicio = T0.AddMinutes(30);

        jornada.RegistrarInterrupcion(Ticket("SUP-9"), inicio, 20, T0.AddMinutes(60));

        var interrupcion = Assert.Single(jornada.Sesiones, s => s.Tipo == TipoSesion.Interrupcion);
        Assert.Equal(inicio.AddMinutes(20), interrupcion.FinUtc);

        // La tarea principal conserva su identidad: solo queda segmentada.
        var abierta = jornada.SesionAbierta!;
        Assert.Equal("SUP-1", abierta.Ticket.TicketId);
        Assert.Equal(inicio.AddMinutes(20), abierta.InicioUtc);
    }

    [Fact]
    public void Interrupcion_NoDejaSolapamientos()
    {
        var jornada = JornadaActiva();
        jornada.RegistrarInterrupcion(Ticket("SUP-9"), T0.AddMinutes(30), 20, T0.AddMinutes(60));

        var orden = jornada.Sesiones.OrderBy(s => s.InicioUtc).ToList();
        for (var i = 1; i < orden.Count; i++)
        {
            Assert.True(
                orden[i].InicioUtc >= orden[i - 1].FinUtc,
                $"El tramo {i} empieza antes de que termine el anterior.");
        }
    }

    [Theory]
    // duración cero o negativa
    [InlineData(30, 0)]
    [InlineData(30, -5)]
    // termina en el futuro
    [InlineData(50, 30)]
    // empieza antes del inicio de la jornada
    [InlineData(-10, 5)]
    public void Interrupcion_RechazaIntervalosInvalidos(int offsetInicio, int duracion)
    {
        var jornada = JornadaActiva();

        var resultado = jornada.RegistrarInterrupcion(
            Ticket("SUP-9"), T0.AddMinutes(offsetInicio), duracion, T0.AddMinutes(60));

        Assert.False(resultado.Ok);
        Assert.Equal(CodigosError.IntervaloInvalido, resultado.Codigo);
    }

    [Fact]
    public void Interrupcion_RechazaSolaparseConUnTramoYaCerrado()
    {
        var jornada = JornadaActiva();
        jornada.FinTarea(Ticket("SUP-2"), T0.AddMinutes(40));

        // Intenta interrumpir en una franja que ya ocupa el primer tramo.
        var resultado = jornada.RegistrarInterrupcion(
            Ticket("SUP-9"), T0.AddMinutes(10), 10, T0.AddMinutes(60));

        Assert.False(resultado.Ok);
        Assert.Equal(CodigosError.IntervaloInvalido, resultado.Codigo);
    }

    // ---------- AC-07 / AC-08 ----------

    [Fact]
    public void SalidaDescanso_CierraLaSesionSinCrearTiempoDeDescanso()
    {
        var jornada = JornadaActiva();

        var resultado = jornada.SalidaDescanso(T0.AddMinutes(60));

        Assert.True(resultado.Ok);
        Assert.Equal(EstadoJornada.EnDescanso, jornada.Estado);
        Assert.DoesNotContain(jornada.Sesiones, s => s.EstaAbierta);
        Assert.Single(jornada.Sesiones);
    }

    [Fact]
    public void RegresoDescanso_ReanudaElMismoTicketPrincipal()
    {
        var jornada = JornadaActiva();
        jornada.SalidaDescanso(T0.AddMinutes(60));

        var resultado = jornada.RegresoDescanso(T0.AddMinutes(95));

        Assert.True(resultado.Ok);
        Assert.Equal(EstadoJornada.Activa, jornada.Estado);
        Assert.Equal("SUP-1", jornada.SesionAbierta!.Ticket.TicketId);
        Assert.Equal(AccionOrigen.RegresoDescanso, jornada.SesionAbierta.AccionOrigen);
    }

    // ---------- AC-09 ----------

    [Fact]
    public void FinDia_CierraLaJornadaYNoAdmiteNuevasAcciones()
    {
        var jornada = JornadaActiva();

        var resultado = jornada.FinDia(T0.AddMinutes(120));

        Assert.True(resultado.Ok);
        Assert.Equal(EstadoJornada.Finalizada, jornada.Estado);
        Assert.Empty(Workday.AccionesHabilitadas(EstadoJornada.Finalizada));
        Assert.False(jornada.SalidaDescanso(T0.AddMinutes(130)).Ok);
        Assert.False(jornada.FinTarea(Ticket("SUP-3"), T0.AddMinutes(130)).Ok);
    }

    [Fact]
    public void FinDia_EnDescanso_ExigeConfirmacionYDataElCierreEnElUltimoTramoReal()
    {
        var jornada = JornadaActiva();
        var salida = T0.AddMinutes(60);
        jornada.SalidaDescanso(salida);

        var sinConfirmar = jornada.FinDia(T0.AddMinutes(90));
        Assert.False(sinConfirmar.Ok);
        Assert.Equal(CodigosError.ConfirmacionRequerida, sinConfirmar.Codigo);
        Assert.Equal(EstadoJornada.EnDescanso, jornada.Estado);

        var confirmado = jornada.FinDia(T0.AddMinutes(90), confirmadoEnDescanso: true);
        Assert.True(confirmado.Ok);
        Assert.Equal(EstadoJornada.Finalizada, jornada.Estado);

        // Durante el descanso no hubo trabajo: la jornada no puede terminar más tarde
        // que el último tramo, o incorporaría tiempo que nadie trabajó.
        Assert.Equal(salida, jornada.FinUtc);
    }

    // ---------- Reapertura (FR-035) ----------

    [Fact]
    public void Reabrir_SinImputar_DejaElIntervaloComoHueco()
    {
        var jornada = JornadaActiva();
        var cierre = T0.AddMinutes(120);
        jornada.FinDia(cierre);

        var reapertura = cierre.AddMinutes(30);
        var resultado = jornada.Reabrir(reapertura, Usuario, "Cierre por error", imputarIntervalo: false);

        Assert.True(resultado.Ok);
        Assert.Equal(EstadoJornada.Activa, jornada.Estado);
        Assert.Null(jornada.FinUtc);
        Assert.Equal(reapertura, jornada.SesionAbierta!.InicioUtc);

        var trabajado = jornada.Sesiones.Aggregate(TimeSpan.Zero, (a, s) => a + s.Duracion(reapertura));
        Assert.Equal(TimeSpan.FromMinutes(120), trabajado);
    }

    [Fact]
    public void Reabrir_Imputando_ArrancaElTramoEnElCierreYSumaElIntervalo()
    {
        var jornada = JornadaActiva();
        var cierre = T0.AddMinutes(120);
        jornada.FinDia(cierre);

        var reapertura = cierre.AddMinutes(30);
        var resultado = jornada.Reabrir(reapertura, Usuario, "Seguí trabajando", imputarIntervalo: true);

        Assert.True(resultado.Ok);
        Assert.Equal(cierre, jornada.SesionAbierta!.InicioUtc);

        var trabajado = jornada.Sesiones.Aggregate(TimeSpan.Zero, (a, s) => a + s.Duracion(reapertura));
        Assert.Equal(TimeSpan.FromMinutes(150), trabajado);
    }

    [Fact]
    public void Reabrir_DejaEntradaDeAuditoria()
    {
        var jornada = JornadaActiva();
        jornada.FinDia(T0.AddMinutes(120));

        jornada.Reabrir(T0.AddMinutes(150), Usuario, "Cierre por error", imputarIntervalo: false);

        var entrada = Assert.Single(jornada.Auditoria);
        Assert.Equal("Reapertura de jornada", entrada.Accion);
        Assert.Contains("Cierre por error", entrada.Detalle);
        Assert.Equal(Usuario, entrada.UserId);
    }

    [Fact]
    public void Reabrir_ExigeMotivo()
    {
        var jornada = JornadaActiva();
        jornada.FinDia(T0.AddMinutes(120));

        var resultado = jornada.Reabrir(T0.AddMinutes(150), Usuario, "   ", imputarIntervalo: false);

        Assert.False(resultado.Ok);
    }

    [Fact]
    public void Reabrir_SoloEsValidoSobreUnaJornadaFinalizada()
    {
        var activa = JornadaActiva();
        Assert.False(activa.Reabrir(T0.AddMinutes(10), Usuario, "x", false).Ok);

        activa.SalidaDescanso(T0.AddMinutes(20));
        Assert.False(activa.Reabrir(T0.AddMinutes(30), Usuario, "x", false).Ok);
    }

    // ---------- Tabla de transiciones completa ----------

    [Theory]
    [InlineData(EstadoJornada.Pendiente, TipoAccion.FinTarea)]
    [InlineData(EstadoJornada.Pendiente, TipoAccion.SalidaDescanso)]
    [InlineData(EstadoJornada.Pendiente, TipoAccion.FinDia)]
    [InlineData(EstadoJornada.Activa, TipoAccion.ComenzarDia)]
    [InlineData(EstadoJornada.Activa, TipoAccion.RegresoDescanso)]
    [InlineData(EstadoJornada.EnDescanso, TipoAccion.FinTarea)]
    [InlineData(EstadoJornada.EnDescanso, TipoAccion.RegistrarInterrupcion)]
    [InlineData(EstadoJornada.EnDescanso, TipoAccion.SalidaDescanso)]
    [InlineData(EstadoJornada.Finalizada, TipoAccion.FinTarea)]
    [InlineData(EstadoJornada.Finalizada, TipoAccion.RegresoDescanso)]
    public void CombinacionesInvalidas_NoAparecenEnLaTabla(EstadoJornada estado, TipoAccion accion)
    {
        var permitidas = Workday.AccionesHabilitadas(estado)
            .Concat(Workday.AccionesCorreccion(estado));

        Assert.DoesNotContain(accion, permitidas);
    }
}
