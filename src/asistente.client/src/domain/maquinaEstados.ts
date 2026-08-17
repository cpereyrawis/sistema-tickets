/**
 * Apoyo de la interfaz para la jornada.
 *
 * Las transiciones YA NO se calculan acá: las decide el backend, que es la autoridad
 * (§6). El cliente recibe el estado y las acciones válidas en cada respuesta.
 *
 * Lo que queda es lo que la interfaz necesita para responder sin esperar al servidor:
 * las etiquetas de los botones y una validación previa del intervalo de interrupción,
 * para poder mostrar el error mientras se escribe (§15.3). Esa validación es un espejo
 * de la del dominio, no un reemplazo: el backend la vuelve a aplicar.
 */

import type { EstadoJornada, Jornada, Sesion, TipoAccion } from './tipos';

export function sesionAbierta(jornada: Jornada): Sesion | undefined {
  return jornada.sesionAbierta ?? jornada.sesiones.find((s) => s.fin === null);
}

export function etiquetaEstado(estado: EstadoJornada): string {
  switch (estado) {
    case 'Pendiente':
      return 'Pendiente de inicio';
    case 'Activa':
      return 'Jornada activa';
    case 'EnDescanso':
      return 'En descanso';
    case 'Finalizada':
      return 'Día finalizado';
  }
}

export const ETIQUETA_ACCION: Record<TipoAccion, string> = {
  ComenzarDia: 'Comenzar el día',
  FinTarea: 'Registrar fin de tarea',
  RegistrarInterrupcion: 'Registrar interrupción',
  SalidaDescanso: 'Registrar salida al descanso',
  // Decisión D-5 del plan: se adopta la alternativa que la propia especificación sugiere.
  RegresoDescanso: 'Registrar regreso del descanso',
  FinDia: 'Registrar fin del día',
  ReabrirJornada: 'Reabrir jornada',
};

/**
 * Espejo de las seis reglas de validación del backend (FR-034), para avisar antes de
 * enviar. Devuelve null si el intervalo es aceptable.
 */
export function validarInterrupcion(
  jornada: Jornada,
  inicio: number,
  duracionMinutos: number,
  ahora: number,
): string | null {
  if (!Number.isFinite(duracionMinutos) || duracionMinutos <= 0) {
    return 'La duración debe ser mayor a cero.';
  }

  const fin = inicio + duracionMinutos * 60_000;

  if (jornada.inicio !== null && inicio < jornada.inicio) {
    return 'La interrupción no puede comenzar antes del inicio de la jornada.';
  }
  if (fin > ahora) {
    return 'La interrupción no puede terminar en el futuro.';
  }

  const actual = sesionAbierta(jornada);
  if (!actual) {
    return 'No hay una tarea principal activa que interrumpir.';
  }
  if (inicio < actual.inicio) {
    return 'La interrupción no puede comenzar antes del tramo de tarea que corta.';
  }

  const solapada = jornada.sesiones.find(
    (s) => s.fin !== null && inicio < s.fin && fin > s.inicio,
  );
  if (solapada) {
    return `El intervalo se solapa con una sesión ya registrada (${solapada.ticket.id}).`;
  }

  return null;
}
