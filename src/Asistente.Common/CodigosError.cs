namespace Asistente.Common;

/// <summary>
/// Códigos de rechazo del dominio. Son parte del contrato con el frontend: la API los
/// traduce a un status HTTP y el cliente decide si reintenta, recarga estado o avisa.
/// </summary>
public static class CodigosError
{
    /// <summary>La acción no es válida para el estado actual de la jornada.</summary>
    public const string AccionNoValida = "ACCION_NO_VALIDA";

    /// <summary>No hay una sesión abierta donde la operación la requiere.</summary>
    public const string SinSesion = "SIN_SESION";

    /// <summary>La jornada no tiene tarea principal para reanudar.</summary>
    public const string SinTareaPrincipal = "SIN_TAREA_PRINCIPAL";

    /// <summary>El intervalo informado se solapa, es negativo o cae fuera de la jornada.</summary>
    public const string IntervaloInvalido = "INTERVALO_INVALIDO";

    /// <summary>La operación necesita una confirmación explícita del usuario.</summary>
    public const string ConfirmacionRequerida = "CONFIRMACION_REQUERIDA";

    /// <summary>Ya existe una jornada abierta para el usuario.</summary>
    public const string JornadaYaAbierta = "JORNADA_YA_ABIERTA";

    /// <summary>La jornada pedida no existe o no pertenece al usuario autenticado.</summary>
    public const string JornadaNoEncontrada = "JORNADA_NO_ENCONTRADA";

    /// <summary>Otro proceso modificó la jornada mientras se preparaba esta operación.</summary>
    public const string ConflictoConcurrencia = "CONFLICTO_CONCURRENCIA";

    /// <summary>El ticket informado no existe en la fuente corporativa.</summary>
    public const string TicketNoEncontrado = "TICKET_NO_ENCONTRADO";

    /// <summary>La fuente corporativa de tickets no respondió.</summary>
    public const string FuenteTicketsNoDisponible = "FUENTE_TICKETS_NO_DISPONIBLE";
}
