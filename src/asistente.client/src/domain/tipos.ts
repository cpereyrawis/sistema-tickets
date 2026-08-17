/**
 * Tipos del dominio de jornada.
 *
 * Reflejan los DTO que devuelve el backend. Los instantes llegan como texto ISO y el
 * cliente de API los convierte a epoch en milisegundos, para que los cálculos de la
 * interfaz trabajen con números y no con strings.
 */

export type EstadoJornada = 'Pendiente' | 'Activa' | 'EnDescanso' | 'Finalizada';

export type TipoSesion = 'Principal' | 'Interrupcion';

export type AccionOrigen =
  | 'ComenzarDia'
  | 'FinTarea'
  | 'RegistrarInterrupcion'
  | 'SalidaDescanso'
  | 'RegresoDescanso'
  | 'FinDia'
  | 'ReabrirJornada';

export type TipoAccion =
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
  id: number;
  ticket: TicketRef;
  tipo: TipoSesion;
  /** epoch ms */
  inicio: number;
  /** epoch ms, null mientras la sesión está abierta */
  fin: number | null;
  accionOrigen: AccionOrigen;
  editada: boolean;
}

export interface EntradaAuditoria {
  accion: string;
  ocurridoEn: number;
  detalle: string;
}

/**
 * Estado de la jornada tal como lo reporta el servidor.
 *
 * Incluye las acciones válidas: la interfaz no las deduce del estado, las recibe. El
 * backend es la autoridad y el cliente se limita a dibujar lo que le dicen.
 */
export interface Jornada {
  id: number | null;
  estado: EstadoJornada;
  fechaLocal: string | null;
  /** epoch ms */
  inicio: number | null;
  fin: number | null;
  ticketPrincipal: TicketRef | null;
  sesionAbierta: Sesion | null;
  sesiones: Sesion[];
  auditoria: EntradaAuditoria[];
  cantidadEventos: number;
  accionesHabilitadas: TipoAccion[];
  accionesCorreccion: TipoAccion[];
  version: number;
}

export interface Usuario {
  id: string;
  usuario: string;
  nombre: string;
}
