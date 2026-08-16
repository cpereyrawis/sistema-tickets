/**
 * Adaptador de consulta de tickets — IMPLEMENTACIÓN SIMULADA.
 *
 * Respeta el contrato `ITicketQueryService` definido en el plan de implementación (§8.1):
 * devuelve DTOs propios, nunca entidades del esquema corporativo. Cuando exista la vista
 * real, se reemplaza esta clase y el resto de la aplicación no cambia.
 *
 * No hay ninguna consulta a base de datos: se filtra un arreglo en memoria y se agrega
 * latencia artificial para que los estados de carga sean visibles (§8.1).
 */

import { CLIENTES, TICKETS } from '../mock/datos';

export interface ClienteDto {
  id: string;
  nombre: string;
  codigo: string;
}

export interface TicketDto {
  externalId: string;
  clienteId: string;
  clienteNombre: string;
  titulo: string;
  estado: string;
  prioridad: string;
  creadoEn: number;
  asignadoA: string;
}

export interface ConsultaTickets {
  clienteId?: string;
  texto?: string;
  pagina?: number;
  tamano?: number;
}

export interface ResultadoPaginado<T> {
  items: T[];
  total: number;
  pagina: number;
  tamano: number;
}

export class FuenteTicketsNoDisponible extends Error {
  constructor() {
    super('No se pudo contactar la fuente de tickets.');
    this.name = 'FuenteTicketsNoDisponible';
  }
}

/** Permite demostrar el estado de error de conexión sin apagar nada. */
let fallaSimulada = false;
export function simularFalla(activo: boolean): void {
  fallaSimulada = activo;
}
export function fallaActiva(): boolean {
  return fallaSimulada;
}

const LATENCIA_MS = 260;

function demora<T>(valor: T): Promise<T> {
  return new Promise((resolve, reject) => {
    setTimeout(() => {
      if (fallaSimulada) reject(new FuenteTicketsNoDisponible());
      else resolve(valor);
    }, LATENCIA_MS);
  });
}

/** Rango Unicode de marcas diacríticas combinantes: U+0300 a U+036F. */
function esDiacritico(ch: string): boolean {
  const c = ch.codePointAt(0) ?? 0;
  return c >= 0x0300 && c <= 0x036f;
}

/** Compara sin distinguir mayúsculas ni acentos: "belgrano" encuentra "Belgráno". */
function normalizar(s: string): string {
  return s
    .toLocaleLowerCase('es')
    .normalize('NFD')
    .split('')
    .filter((ch) => !esDiacritico(ch))
    .join('');
}

export async function buscarClientes(termino: string): Promise<ClienteDto[]> {
  const t = normalizar(termino.trim());
  const items = CLIENTES.filter((c) => c.ACTIVO)
    .filter(
      (c) =>
        t === '' ||
        normalizar(c.CLIENTE_NOMBRE).includes(t) ||
        normalizar(c.CLIENTE_CODIGO).includes(t),
    )
    .map<ClienteDto>((c) => ({
      id: c.CLIENTE_ID,
      nombre: c.CLIENTE_NOMBRE,
      codigo: c.CLIENTE_CODIGO,
    }));
  return demora(items);
}

export async function buscarTickets(
  consulta: ConsultaTickets,
): Promise<ResultadoPaginado<TicketDto>> {
  const pagina = consulta.pagina ?? 1;
  const tamano = consulta.tamano ?? 8;
  const t = normalizar((consulta.texto ?? '').trim());

  const filtrados = TICKETS.filter((r) => {
    if (consulta.clienteId && r.CLIENTE_ID !== consulta.clienteId) return false;
    if (t === '') return true;
    return (
      normalizar(r.TICKET_ID).includes(t) ||
      normalizar(r.TITULO).includes(t) ||
      normalizar(r.CLIENTE_NOMBRE).includes(t)
    );
  })
    // Orden descendente por fecha de creación: requisito FR-011 / AC-10.
    .sort(
      (a, b) =>
        new Date(b.FECHA_CREACION).getTime() - new Date(a.FECHA_CREACION).getTime(),
    );

  const desde = (pagina - 1) * tamano;
  const items = filtrados.slice(desde, desde + tamano).map<TicketDto>((r) => ({
    externalId: r.TICKET_ID,
    clienteId: r.CLIENTE_ID,
    clienteNombre: r.CLIENTE_NOMBRE,
    titulo: r.TITULO,
    estado: r.ESTADO,
    prioridad: r.PRIORIDAD,
    creadoEn: new Date(r.FECHA_CREACION).getTime(),
    asignadoA: r.ASIGNADO_A,
  }));

  return demora({ items, total: filtrados.length, pagina, tamano });
}
