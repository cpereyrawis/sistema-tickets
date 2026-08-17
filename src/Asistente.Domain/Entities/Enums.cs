namespace Asistente.Domain.Entities;

/// <summary>Estados de la jornada (§6 de la especificación).</summary>
public enum EstadoJornada
{
    Pendiente = 0,
    Activa = 1,
    EnDescanso = 2,
    Finalizada = 3,
}

public enum TipoSesion
{
    Principal = 0,
    Interrupcion = 1,
}

public enum TipoEvento
{
    InicioPrincipal = 0,
    FinPrincipal = 1,
    InicioInterrupcion = 2,
    FinInterrupcion = 3,
}

/// <summary>Acción que originó una sesión. Permite explicar los huecos de la jornada.</summary>
public enum AccionOrigen
{
    ComenzarDia = 0,
    FinTarea = 1,
    RegistrarInterrupcion = 2,
    SalidaDescanso = 3,
    RegresoDescanso = 4,
    FinDia = 5,
    ReabrirJornada = 6,
}

/// <summary>Acciones que la máquina de estados acepta.</summary>
public enum TipoAccion
{
    ComenzarDia = 0,
    FinTarea = 1,
    RegistrarInterrupcion = 2,
    SalidaDescanso = 3,
    RegresoDescanso = 4,
    FinDia = 5,
    ReabrirJornada = 6,
}
