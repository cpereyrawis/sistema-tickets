/**
 * Cliente HTTP del backend.
 *
 * Además de llamar, adapta: convierte los instantes ISO del servidor a epoch en
 * milisegundos y aplana los nombres de los DTO a los del dominio del cliente. Toda esa
 * traducción vive acá, de modo que los componentes no se enteran de la forma del JSON.
 */

import type { Jornada, Sesion, TicketRef, TipoAccion } from '../domain/tipos';

const BASE = '/api';

/** Error con el código estable que devuelve el dominio del backend. */
export class ErrorApi extends Error {
  constructor(
    public readonly codigo: string,
    mensaje: string,
    public readonly status: number,
  ) {
    super(mensaje);
    this.name = 'ErrorApi';
  }
}

/** Permite demostrar el estado de error de conexión sin apagar el backend. */
let fallaSimulada = false;
export function simularFalla(activo: boolean): void {
  fallaSimulada = activo;
}
export function fallaActiva(): boolean {
  return fallaSimulada;
}

/** Usuario que se envía al backend mientras no exista autenticación real. */
let usuarioActual: string | null = null;
export function fijarUsuario(id: string | null): void {
  usuarioActual = id;
}

async function pedir<T>(ruta: string, init?: RequestInit): Promise<T> {
  if (fallaSimulada) {
    throw new ErrorApi('FUENTE_NO_DISPONIBLE', 'Fuente simulada como caída.', 503);
  }

  const cabeceras: Record<string, string> = { 'Content-Type': 'application/json' };
  if (usuarioActual) {
    // Identidad de desarrollo. Cuando exista la cookie corporativa esta cabecera
    // desaparece y no cambia nada más de este archivo.
    cabeceras['X-Usuario-Id'] = usuarioActual;
  }

  const respuesta = await fetch(`${BASE}${ruta}`, {
    ...init,
    headers: { ...cabeceras, ...(init?.headers ?? {}) },
  });

  if (!respuesta.ok) {
    // El backend responde ProblemDetails: `type` lleva el código del dominio y `title`
    // el mensaje ya redactado para el usuario.
    let codigo = `HTTP_${respuesta.status}`;
    let mensaje = 'No se pudo completar la operación.';
    try {
      const problema = await respuesta.json();
      codigo = problema.type ?? codigo;
      mensaje = problema.title ?? mensaje;
    } catch {
      // Respuesta sin cuerpo JSON: se conserva el mensaje genérico.
    }
    throw new ErrorApi(codigo, mensaje, respuesta.status);
  }

  if (respuesta.status === 204) return undefined as T;
  return (await respuesta.json()) as T;
}

// ---------- Adaptación de instantes ----------

const aMs = (iso: string): number => new Date(iso).getTime();
const aMsOpcional = (iso: string | null): number | null => (iso === null ? null : aMs(iso));

interface TicketRefDto {
  ticketId: string;
  clienteId: string;
  clienteNombre: string;
  titulo: string;
}

interface SesionDto {
  id: number;
  ticket: TicketRefDto;
  tipo: Sesion['tipo'];
  inicioUtc: string;
  finUtc: string | null;
  accionOrigen: Sesion['accionOrigen'];
  editada: boolean;
}

interface EstadoJornadaDto {
  jornadaId: number | null;
  estado: Jornada['estado'];
  fechaLocal: string | null;
  inicioUtc: string | null;
  finUtc: string | null;
  ticketPrincipal: TicketRefDto | null;
  sesionAbierta: SesionDto | null;
  sesiones: SesionDto[];
  auditoria: { accion: string; ocurridoEnUtc: string; detalle: string }[];
  cantidadEventos: number;
  accionesHabilitadas: TipoAccion[];
  accionesCorreccion: TipoAccion[];
  version: number;
}

function mapearTicket(t: TicketRefDto): TicketRef {
  return {
    id: t.ticketId,
    clienteId: t.clienteId,
    clienteNombre: t.clienteNombre,
    titulo: t.titulo,
  };
}

function mapearSesion(s: SesionDto): Sesion {
  return {
    id: s.id,
    ticket: mapearTicket(s.ticket),
    tipo: s.tipo,
    inicio: aMs(s.inicioUtc),
    fin: aMsOpcional(s.finUtc),
    accionOrigen: s.accionOrigen,
    editada: s.editada,
  };
}

function mapearJornada(d: EstadoJornadaDto): Jornada {
  return {
    id: d.jornadaId,
    estado: d.estado,
    fechaLocal: d.fechaLocal,
    inicio: aMsOpcional(d.inicioUtc),
    fin: aMsOpcional(d.finUtc),
    ticketPrincipal: d.ticketPrincipal ? mapearTicket(d.ticketPrincipal) : null,
    sesionAbierta: d.sesionAbierta ? mapearSesion(d.sesionAbierta) : null,
    sesiones: d.sesiones.map(mapearSesion),
    auditoria: d.auditoria.map((a) => ({
      accion: a.accion,
      ocurridoEn: aMs(a.ocurridoEnUtc),
      detalle: a.detalle,
    })),
    cantidadEventos: d.cantidadEventos,
    accionesHabilitadas: d.accionesHabilitadas,
    accionesCorreccion: d.accionesCorreccion,
    version: d.version,
  };
}

const postJornada = (ruta: string, cuerpo?: unknown) =>
  pedir<EstadoJornadaDto>(`/jornada${ruta}`, {
    method: 'POST',
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  }).then(mapearJornada);

// ---------- Jornada ----------

export const jornadaApi = {
  actual: () => pedir<EstadoJornadaDto>('/jornada/actual').then(mapearJornada),

  comenzar: (ticketId: string) => postJornada('/comenzar', { ticketId }),

  finTarea: (ticketId: string) => postJornada('/fin-tarea', { ticketId }),

  interrupcion: (ticketId: string, inicioMs: number, duracionMinutos: number) =>
    postJornada('/interrupcion', {
      ticketId,
      inicioUtc: new Date(inicioMs).toISOString(),
      duracionMinutos,
    }),

  salidaDescanso: () => postJornada('/descanso/salida'),

  regresoDescanso: () => postJornada('/descanso/regreso'),

  finDia: (confirmadoEnDescanso = false) => postJornada('/fin-dia', { confirmadoEnDescanso }),

  reabrir: (motivo: string, imputarIntervalo: boolean) =>
    postJornada('/reabrir', { motivo, imputarIntervalo }),
};

// ---------- Tickets ----------

export interface ClienteApi {
  id: string;
  nombre: string;
  codigo: string;
}

export interface TicketApi {
  ticketId: string;
  clienteId: string;
  clienteNombre: string;
  titulo: string;
  estado: string;
  prioridad: string;
  creadoEn: number;
  asignadoA: string;
}

export interface PaginaApi<T> {
  items: T[];
  total: number;
  pagina: number;
  tamano: number;
}

export const ticketsApi = {
  clientes: () => pedir<ClienteApi[]>('/tickets/clientes?maximo=200'),

  buscar: async (opciones: {
    clienteId?: string;
    texto?: string;
    pagina: number;
    tamano: number;
  }): Promise<PaginaApi<TicketApi>> => {
    const params = new URLSearchParams({
      pagina: String(opciones.pagina),
      tamano: String(opciones.tamano),
    });
    if (opciones.clienteId) params.set('clienteId', opciones.clienteId);
    if (opciones.texto) params.set('q', opciones.texto);

    const pagina = await pedir<PaginaApi<TicketApi & { creadoEnUtc: string }>>(
      `/tickets?${params}`,
    );

    return {
      ...pagina,
      items: pagina.items.map((t) => ({ ...t, creadoEn: aMs(t.creadoEnUtc) })),
    };
  },
};

// ---------- Utilidades de desarrollo ----------

export const devApi = {
  /** Siembra una jornada de ejemplo con tramos, interrupción y descanso. Solo en desarrollo. */
  jornadaEjemplo: () =>
    pedir<EstadoJornadaDto>('/dev/jornada-ejemplo', { method: 'POST' }).then(mapearJornada),

  /** Borra las jornadas del usuario para volver a empezar. Solo en desarrollo. */
  reiniciar: () => pedir<void>('/dev/jornada', { method: 'DELETE' }),
};
