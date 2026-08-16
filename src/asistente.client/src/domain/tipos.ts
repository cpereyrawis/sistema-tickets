/**
 * Tipos del dominio de jornada.
 * Reflejan el modelo de datos de la sección 13 de la especificación, adaptados al prototipo.
 * Todos los instantes se guardan como epoch en milisegundos (equivalente a UTC).
 */

export type EstadoJornada = 'Pendiente' | 'Activa' | 'EnDescanso' | 'Finalizada';

export type TipoSesion = 'Principal' | 'Interrupcion';

export type TipoEvento =
  | 'InicioPrincipal'
  | 'FinPrincipal'
  | 'InicioInterrupcion'
  | 'FinInterrupcion';

export type AccionOrigen =
  | 'ComenzarDia'
  | 'FinTarea'
  | 'RegistrarInterrupcion'
  | 'SalidaDescanso'
  | 'RegresoDescanso'
  | 'FinDia'
  | 'ReabrirJornada';

export interface TicketRef {
  id: string;
  clienteId: string;
  clienteNombre: string;
  titulo: string;
}

export interface Sesion {
  id: string;
  ticket: TicketRef;
  tipo: TipoSesion;
  /** epoch ms */
  inicio: number;
  /** epoch ms, null mientras la sesión está abierta */
  fin: number | null;
  accionOrigen: AccionOrigen;
  editada: boolean;
}

export interface Evento {
  id: string;
  ticketId: string;
  tipo: TipoEvento;
  ocurridoEn: number;
  /** Comparten CorrelationId los cuatro eventos de una interrupción (§13.1). */
  correlationId: string;
  creadoEn: number;
}

export interface Jornada {
  id: string;
  usuarioId: string;
  /** Fecha local de la jornada; se fija al comenzar el día y no cambia aunque cruce medianoche. */
  fechaLocal: string;
  inicio: number;
  fin: number | null;
  estado: EstadoJornada;
  ticketPrincipal: TicketRef | null;
  sesiones: Sesion[];
  eventos: Evento[];
  auditoria: EntradaAuditoria[];
}

/**
 * Corrección manual registrada sobre la jornada (FR-035, NFR-007).
 * Es append-only: nunca se borra ni se edita, para que la trazabilidad no se rompa.
 */
export interface EntradaAuditoria {
  id: string;
  accion: string;
  ocurridoEn: number;
  detalle: string;
}

export interface Usuario {
  id: string;
  usuario: string;
  nombre: string;
}

/** Acciones que puede recibir la máquina de estados. */
export type Accion =
  | { tipo: 'ComenzarDia'; ticket: TicketRef; ahora: number }
  | { tipo: 'FinTarea'; ticket: TicketRef; ahora: number }
  | {
      tipo: 'RegistrarInterrupcion';
      ticket: TicketRef;
      inicio: number;
      duracionMinutos: number;
      ahora: number;
    }
  | { tipo: 'SalidaDescanso'; ahora: number }
  | { tipo: 'RegresoDescanso'; ahora: number }
  | { tipo: 'FinDia'; ahora: number; confirmadoEnDescanso?: boolean }
  /** Corrección: revierte un cierre equivocado. No es una acción operativa normal. */
  | {
      tipo: 'ReabrirJornada';
      ahora: number;
      motivo: string;
      /**
       * Si es true, el tramo nuevo arranca en el momento del cierre, de modo que el
       * intervalo transcurrido se computa como trabajo sobre la tarea principal.
       * Si es false, ese intervalo queda como hueco sin imputar.
       */
      imputarIntervalo: boolean;
    };

export type TipoAccion = Accion['tipo'];

export type Resultado<T> =
  | { ok: true; valor: T }
  | { ok: false; codigo: string; mensaje: string };
