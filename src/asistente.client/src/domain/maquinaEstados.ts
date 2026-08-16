/**
 * Máquina de estados de la jornada.
 *
 * Función pura: recibe la jornada vigente y una acción, devuelve una jornada nueva
 * o un error. No toca red, almacenamiento ni React. Implementa la tabla de transiciones
 * de la sección 6 de la especificación y los invariantes de §6.1.
 *
 * En el sistema real esta lógica vive en el backend (Asistente.Domain) y el navegador
 * solo la usa para habilitar botones; aquí corre en el cliente porque el prototipo
 * no tiene servidor.
 */

import type {
  Accion,
  EstadoJornada,
  Evento,
  Jornada,
  Resultado,
  Sesion,
  TicketRef,
  TipoAccion,
  TipoEvento,
  Usuario,
} from './tipos';

let contador = 0;
function nuevoId(prefijo: string): string {
  contador += 1;
  return `${prefijo}-${Date.now().toString(36)}-${contador.toString(36)}`;
}

/** Acciones habilitadas por estado. La UI oculta el resto; el dominio igual las rechaza. */
export function accionesHabilitadas(estado: EstadoJornada): TipoAccion[] {
  switch (estado) {
    case 'Pendiente':
      return ['ComenzarDia'];
    case 'Activa':
      return ['FinTarea', 'RegistrarInterrupcion', 'SalidaDescanso', 'FinDia'];
    case 'EnDescanso':
      return ['RegresoDescanso', 'FinDia'];
    case 'Finalizada':
      return [];
  }
}

export function sesionAbierta(jornada: Jornada): Sesion | undefined {
  return jornada.sesiones.find((s) => s.fin === null);
}

export function fechaLocalDe(instante: number): string {
  const d = new Date(instante);
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${dd}`;
}

function evento(
  tipo: TipoEvento,
  ticketId: string,
  ocurridoEn: number,
  correlationId: string,
): Evento {
  return {
    id: nuevoId('ev'),
    tipo,
    ticketId,
    ocurridoEn,
    correlationId,
    creadoEn: Date.now(),
  };
}

function abrirSesion(
  ticket: TicketRef,
  inicio: number,
  tipo: Sesion['tipo'],
  accionOrigen: Sesion['accionOrigen'],
): Sesion {
  return {
    id: nuevoId('ses'),
    ticket,
    tipo,
    inicio,
    fin: null,
    accionOrigen,
    editada: false,
  };
}

function error(codigo: string, mensaje: string): Resultado<Jornada> {
  return { ok: false, codigo, mensaje };
}

/**
 * Valida una interrupción según las seis reglas del plan de implementación (§5.4).
 * Devuelve null si es válida, o el mensaje de rechazo.
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

  if (inicio < jornada.inicio) {
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

/** Aplica una acción a la jornada. Devuelve una jornada nueva; nunca muta la recibida. */
export function aplicar(
  jornada: Jornada | null,
  accion: Accion,
  usuario: Usuario,
): Resultado<Jornada> {
  const estado: EstadoJornada = jornada?.estado ?? 'Pendiente';

  // El dominio revalida siempre, aunque la interfaz haya ocultado el botón (§6).
  if (!accionesHabilitadas(estado).includes(accion.tipo)) {
    return error(
      'ACCION_NO_VALIDA',
      `La acción no es válida en el estado "${etiquetaEstado(estado)}".`,
    );
  }

  switch (accion.tipo) {
    case 'ComenzarDia': {
      const corr = nuevoId('corr');
      const sesion = abrirSesion(accion.ticket, accion.ahora, 'Principal', 'ComenzarDia');
      const nueva: Jornada = {
        id: nuevoId('jor'),
        usuarioId: usuario.id,
        fechaLocal: fechaLocalDe(accion.ahora),
        inicio: accion.ahora,
        fin: null,
        estado: 'Activa',
        ticketPrincipal: accion.ticket,
        sesiones: [sesion],
        eventos: [evento('InicioPrincipal', accion.ticket.id, accion.ahora, corr)],
      };
      return { ok: true, valor: nueva };
    }

    case 'FinTarea': {
      const j = jornada!;
      const actual = sesionAbierta(j);
      if (!actual) return error('SIN_SESION', 'No hay una sesión abierta para cerrar.');

      // Cierre y apertura comparten la misma marca temporal (AC-03), en una sola transición.
      const corr = nuevoId('corr');
      const t = accion.ahora;
      return {
        ok: true,
        valor: {
          ...j,
          ticketPrincipal: accion.ticket,
          sesiones: [
            ...j.sesiones.map((s) => (s.id === actual.id ? { ...s, fin: t } : s)),
            abrirSesion(accion.ticket, t, 'Principal', 'FinTarea'),
          ],
          eventos: [
            ...j.eventos,
            evento('FinPrincipal', actual.ticket.id, t, corr),
            evento('InicioPrincipal', accion.ticket.id, t, corr),
          ],
        },
      };
    }

    case 'RegistrarInterrupcion': {
      const j = jornada!;
      const actual = sesionAbierta(j);
      if (!actual) return error('SIN_SESION', 'No hay una tarea principal activa.');

      const problema = validarInterrupcion(
        j,
        accion.inicio,
        accion.duracionMinutos,
        accion.ahora,
      );
      if (problema) return error('INTERVALO_INVALIDO', problema);

      const inicio = accion.inicio;
      const fin = inicio + accion.duracionMinutos * 60_000;
      const principal = actual.ticket;
      // Los cuatro eventos comparten CorrelationId (§13.1) y se aplican de una sola vez.
      const corr = nuevoId('corr');

      return {
        ok: true,
        valor: {
          ...j,
          sesiones: [
            ...j.sesiones.map((s) => (s.id === actual.id ? { ...s, fin: inicio } : s)),
            {
              ...abrirSesion(accion.ticket, inicio, 'Interrupcion', 'RegistrarInterrupcion'),
              fin,
            },
            // La tarea principal conserva su identidad: solo queda segmentada (§7.3).
            abrirSesion(principal, fin, 'Principal', 'RegistrarInterrupcion'),
          ],
          eventos: [
            ...j.eventos,
            evento('FinPrincipal', principal.id, inicio, corr),
            evento('InicioInterrupcion', accion.ticket.id, inicio, corr),
            evento('FinInterrupcion', accion.ticket.id, fin, corr),
            evento('InicioPrincipal', principal.id, fin, corr),
          ],
        },
      };
    }

    case 'SalidaDescanso': {
      const j = jornada!;
      const actual = sesionAbierta(j);
      if (!actual) return error('SIN_SESION', 'No hay una sesión abierta para cerrar.');

      const corr = nuevoId('corr');
      // Solo cierra la sesión principal: no crea tarea de descanso (§7.4, AC-07).
      return {
        ok: true,
        valor: {
          ...j,
          estado: 'EnDescanso',
          sesiones: j.sesiones.map((s) =>
            s.id === actual.id ? { ...s, fin: accion.ahora } : s,
          ),
          eventos: [...j.eventos, evento('FinPrincipal', actual.ticket.id, accion.ahora, corr)],
        },
      };
    }

    case 'RegresoDescanso': {
      const j = jornada!;
      const principal = j.ticketPrincipal;
      if (!principal) return error('SIN_PRINCIPAL', 'No hay tarea principal para reanudar.');

      const corr = nuevoId('corr');
      // Reanuda el MISMO ticket principal (§7.5, AC-08).
      return {
        ok: true,
        valor: {
          ...j,
          estado: 'Activa',
          sesiones: [
            ...j.sesiones,
            abrirSesion(principal, accion.ahora, 'Principal', 'RegresoDescanso'),
          ],
          eventos: [...j.eventos, evento('InicioPrincipal', principal.id, accion.ahora, corr)],
        },
      };
    }

    case 'FinDia': {
      const j = jornada!;
      const actual = sesionAbierta(j);
      const corr = nuevoId('corr');

      if (j.estado === 'EnDescanso') {
        // Decisión D-6 del plan: se cierra sin crear una reanudación artificial (§7.6),
        // pero exige confirmación explícita del usuario.
        if (!accion.confirmadoEnDescanso) {
          return error(
            'CONFIRMACION_REQUERIDA',
            'La jornada está en descanso. Confirmá que querés cerrarla sin reanudar la tarea.',
          );
        }
        // La jornada termina cuando terminó el último tramo real, no cuando se pulsó
        // el botón: durante el descanso no hubo trabajo, y datar el cierre en el clic
        // agregaría al tramo final un intervalo que nadie trabajó.
        const ultimoCierre = j.sesiones.reduce(
          (max, s) => Math.max(max, s.fin ?? 0),
          j.inicio,
        );
        return {
          ok: true,
          valor: { ...j, estado: 'Finalizada', fin: ultimoCierre },
        };
      }

      if (!actual) return error('SIN_SESION', 'No hay una sesión abierta para cerrar.');
      return {
        ok: true,
        valor: {
          ...j,
          estado: 'Finalizada',
          fin: accion.ahora,
          sesiones: j.sesiones.map((s) =>
            s.id === actual.id ? { ...s, fin: accion.ahora } : s,
          ),
          eventos: [...j.eventos, evento('FinPrincipal', actual.ticket.id, accion.ahora, corr)],
        },
      };
    }
  }
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
};
