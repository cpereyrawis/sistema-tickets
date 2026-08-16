/**
 * Cálculos derivados de la jornada: duraciones, agrupación por ticket,
 * detección de anomalías y filas de exportación.
 * Puro: no depende de React ni de almacenamiento.
 */

import type { Jornada, Sesion } from './tipos';

export function duracionMs(sesion: Sesion, ahora: number): number {
  return (sesion.fin ?? ahora) - sesion.inicio;
}

export function formatearDuracion(ms: number, conSegundos = false): string {
  const total = Math.max(0, Math.floor(ms / 1000));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const hh = String(h).padStart(2, '0');
  const mm = String(m).padStart(2, '0');
  if (!conSegundos) return `${hh}:${mm}`;
  return `${hh}:${mm}:${String(s).padStart(2, '0')}`;
}

export function formatearHora(instante: number): string {
  return new Date(instante).toLocaleTimeString('es-AR', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });
}

export function formatearFecha(instante: number): string {
  return new Date(instante).toLocaleDateString('es-AR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });
}

export function formatearFechaLarga(instante: number): string {
  return new Date(instante).toLocaleDateString('es-AR', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  });
}

/** Sesiones ordenadas cronológicamente. */
export function sesionesOrdenadas(jornada: Jornada): Sesion[] {
  return [...jornada.sesiones].sort((a, b) => a.inicio - b.inicio);
}

/** Tiempo total registrado en la jornada, contando la sesión abierta hasta `ahora`. */
export function totalTrabajado(jornada: Jornada, ahora: number): number {
  return jornada.sesiones.reduce((acc, s) => acc + duracionMs(s, ahora), 0);
}

export interface AgrupadoPorTicket {
  ticketId: string;
  clienteNombre: string;
  titulo: string;
  tramos: number;
  totalMs: number;
  tieneInterrupcion: boolean;
}

/** Vista agrupada por ticket (FR-040). */
export function agruparPorTicket(jornada: Jornada, ahora: number): AgrupadoPorTicket[] {
  const mapa = new Map<string, AgrupadoPorTicket>();
  for (const s of sesionesOrdenadas(jornada)) {
    const previo = mapa.get(s.ticket.id);
    const ms = duracionMs(s, ahora);
    if (previo) {
      previo.tramos += 1;
      previo.totalMs += ms;
      previo.tieneInterrupcion ||= s.tipo === 'Interrupcion';
    } else {
      mapa.set(s.ticket.id, {
        ticketId: s.ticket.id,
        clienteNombre: s.ticket.clienteNombre,
        titulo: s.ticket.titulo,
        tramos: 1,
        totalMs: ms,
        tieneInterrupcion: s.tipo === 'Interrupcion',
      });
    }
  }
  return [...mapa.values()].sort((a, b) => b.totalMs - a.totalMs);
}

/**
 * Por qué existe el hueco que precede a una sesión, si es que es esperable.
 * Devuelve null cuando el hueco no tiene explicación y debe reportarse como anomalía.
 */
export function causaHueco(sesion: Sesion): 'descanso' | 'reapertura' | null {
  if (sesion.accionOrigen === 'RegresoDescanso') return 'descanso';
  if (sesion.accionOrigen === 'ReabrirJornada') return 'reapertura';
  return null;
}

export const ETIQUETA_HUECO: Record<'descanso' | 'reapertura', string> = {
  descanso: 'Descanso — sin tiempo imputado',
  reapertura: 'Jornada reabierta — intervalo sin imputar',
};

export interface Anomalia {
  tipo: 'hueco' | 'solapamiento';
  mensaje: string;
  desde: number;
  hasta: number;
}

/**
 * Detecta huecos y solapamientos (FR-041).
 * Los descansos y las reaperturas de jornada producen huecos legítimos: se reconocen
 * por la acción que originó la sesión siguiente y no se reportan como anomalía.
 */
export function detectarAnomalias(jornada: Jornada): Anomalia[] {
  const orden = sesionesOrdenadas(jornada).filter((s) => s.fin !== null);
  const out: Anomalia[] = [];

  // Invariante de §6.1: el fin de una sesión nunca puede ser anterior a su inicio.
  // El dominio no puede producirlo, pero un dato guardado o migrado sí: conviene
  // gritarlo en la revisión antes de que se cuele en el Excel.
  for (const s of orden) {
    if ((s.fin as number) < s.inicio) {
      out.push({
        tipo: 'solapamiento',
        desde: s.fin as number,
        hasta: s.inicio,
        mensaje: `El tramo de ${s.ticket.id} termina (${formatearHora(
          s.fin as number,
        )}) antes de empezar (${formatearHora(s.inicio)}). El registro está corrupto.`,
      });
    }
  }

  for (let i = 1; i < orden.length; i += 1) {
    const previa = orden[i - 1];
    const actual = orden[i];
    const finPrevia = previa.fin as number;

    if (actual.inicio > finPrevia) {
      if (causaHueco(actual) !== null) continue; // hueco esperado y explicado
      out.push({
        tipo: 'hueco',
        desde: finPrevia,
        hasta: actual.inicio,
        mensaje: `Hueco sin registrar entre ${formatearHora(finPrevia)} y ${formatearHora(actual.inicio)}.`,
      });
    } else if (actual.inicio < finPrevia) {
      out.push({
        tipo: 'solapamiento',
        desde: actual.inicio,
        hasta: finPrevia,
        mensaje: `Solapamiento entre ${previa.ticket.id} y ${actual.ticket.id}.`,
      });
    }
  }
  return out;
}

export interface FilaExportacion {
  fecha: string;
  ticket: string;
  cliente: string;
  inicio: string;
  fin: string;
  duracion: string;
  tipo: string;
  motivo: string;
}

/**
 * Filas que se escribirían en el Excel (§14.2).
 *
 * PROVISIONAL: el perfil real depende de la plantilla corporativa, que todavía no se
 * relevó (decisión D-7 del plan). Sin agrupación ni redondeo hasta confirmarla.
 */
export function filasExportacion(jornada: Jornada): FilaExportacion[] {
  return sesionesOrdenadas(jornada)
    .filter((s) => s.fin !== null)
    .map((s) => ({
      fecha: formatearFecha(s.inicio),
      ticket: s.ticket.id,
      cliente: s.ticket.clienteNombre,
      inicio: formatearHora(s.inicio),
      fin: formatearHora(s.fin as number),
      duracion: formatearDuracion(duracionMs(s, s.fin as number)),
      tipo: s.tipo === 'Principal' ? 'Principal' : 'Interrupción',
      motivo: s.tipo === 'Interrupcion' ? s.ticket.titulo : '',
    }));
}

export function nombreArchivoExcel(jornada: Jornada, usuario: string): string {
  return `registro_${usuario}_${jornada.fechaLocal}.xlsx`;
}
