/**
 * ESQUEMA SIMULADO — PROTOTIPO VISUAL
 *
 * Este módulo documenta las tablas y columnas que el prototipo ASUME que existirán.
 * NO se ejecuta ninguna consulta real: todos los datos viven en memoria (ver `datos.ts`).
 *
 * Cuando se complete la Fase 0 del plan de implementación (relevamiento de la fuente
 * corporativa), estas definiciones deben reemplazarse por el esquema real de la vista
 * autorizada. El único punto de cambio es `services/ticketQueryService.ts`.
 */

export interface ColumnaSimulada {
  nombre: string;
  tipo: string;
  nota?: string;
}

export interface TablaSimulada {
  nombre: string;
  origen: 'Sistema de tickets (solo lectura)' | 'Base del asistente (lectura/escritura)';
  descripcion: string;
  columnas: ColumnaSimulada[];
}

export const ESQUEMA_SIMULADO: TablaSimulada[] = [
  {
    nombre: 'vw_asistente_clientes',
    origen: 'Sistema de tickets (solo lectura)',
    descripcion:
      'Vista de clientes habilitada para el asistente. Solo los campos necesarios para filtrar.',
    columnas: [
      { nombre: 'CLIENTE_ID', tipo: 'varchar(16)', nota: 'Clave de negocio, ej. CLI-004' },
      { nombre: 'CLIENTE_NOMBRE', tipo: 'nvarchar(120)' },
      { nombre: 'CLIENTE_CODIGO', tipo: 'varchar(24)', nota: 'Código corto para búsqueda' },
      { nombre: 'ACTIVO', tipo: 'bit' },
    ],
  },
  {
    nombre: 'vw_asistente_tickets',
    origen: 'Sistema de tickets (solo lectura)',
    descripcion:
      'Vista de tickets habilitada para el asistente. La cuenta técnica solo tiene permiso SELECT.',
    columnas: [
      { nombre: 'TICKET_ID', tipo: 'varchar(24)', nota: 'Identificador visible, ej. SUP-12345' },
      { nombre: 'CLIENTE_ID', tipo: 'varchar(16)' },
      { nombre: 'CLIENTE_NOMBRE', tipo: 'nvarchar(120)', nota: 'Desnormalizado para evitar un join' },
      { nombre: 'TITULO', tipo: 'nvarchar(200)' },
      { nombre: 'ESTADO', tipo: 'varchar(32)', nota: 'Abierto | En curso | Pendiente cliente | Resuelto | Cerrado' },
      { nombre: 'PRIORIDAD', tipo: 'varchar(16)', nota: 'Alta | Media | Baja' },
      { nombre: 'FECHA_CREACION', tipo: 'datetime2', nota: 'Orden descendente obligatorio (FR-011)' },
      { nombre: 'ASIGNADO_A', tipo: 'nvarchar(120)' },
    ],
  },
  {
    nombre: 'vw_asistente_usuarios',
    origen: 'Sistema de tickets (solo lectura)',
    descripcion:
      'Identidades reutilizables. El prototipo NO usa SSO: valida contra esta lista simulada. ' +
      'En el sistema real nunca se leen ni almacenan contraseñas en texto plano (FR-003).',
    columnas: [
      { nombre: 'USUARIO_ID', tipo: 'varchar(16)', nota: 'Se mapea a AppUser.ExternalUserId' },
      { nombre: 'USUARIO', tipo: 'varchar(64)', nota: 'Nombre de inicio de sesión' },
      { nombre: 'NOMBRE_COMPLETO', tipo: 'nvarchar(120)' },
      { nombre: 'ACTIVO', tipo: 'bit' },
    ],
  },
  {
    nombre: 'Workday / WorkSession / TimeEvent',
    origen: 'Base del asistente (lectura/escritura)',
    descripcion:
      'En el prototipo estas entidades viven en memoria del navegador (con respaldo en localStorage ' +
      'para poder demostrar que recargar la página conserva el estado, AC-12).',
    columnas: [
      { nombre: 'Workday', tipo: 'entidad', nota: 'Id, UsuarioId, FechaLocal, Inicio, Fin, Estado, TicketPrincipal' },
      { nombre: 'WorkSession', tipo: 'entidad', nota: 'Id, Ticket, Tipo, Inicio, Fin, AccionOrigen, Editada' },
      { nombre: 'TimeEvent', tipo: 'entidad', nota: 'Id, TicketId, Tipo, OcurridoEn, CorrelationId' },
    ],
  },
];
