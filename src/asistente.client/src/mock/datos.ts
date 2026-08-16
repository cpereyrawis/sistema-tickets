/**
 * DATOS SIMULADOS — PROTOTIPO VISUAL
 *
 * Filas de ejemplo con la forma exacta que tendrían las vistas corporativas
 * descritas en `esquema.ts`. Todo vive en memoria; no hay ninguna consulta a base de datos.
 */

export interface FilaClienteCorporativo {
  CLIENTE_ID: string;
  CLIENTE_NOMBRE: string;
  CLIENTE_CODIGO: string;
  ACTIVO: boolean;
}

export interface FilaTicketCorporativo {
  TICKET_ID: string;
  CLIENTE_ID: string;
  CLIENTE_NOMBRE: string;
  TITULO: string;
  ESTADO: string;
  PRIORIDAD: string;
  FECHA_CREACION: string; // ISO 8601
  ASIGNADO_A: string;
}

export interface FilaUsuarioCorporativo {
  USUARIO_ID: string;
  USUARIO: string;
  NOMBRE_COMPLETO: string;
  ACTIVO: boolean;
}

export const USUARIOS: FilaUsuarioCorporativo[] = [
  { USUARIO_ID: 'U-1001', USUARIO: 'cpereyra', NOMBRE_COMPLETO: 'Cristian Pereyra', ACTIVO: true },
  { USUARIO_ID: 'U-1002', USUARIO: 'mlopez', NOMBRE_COMPLETO: 'Marina López', ACTIVO: true },
  { USUARIO_ID: 'U-1003', USUARIO: 'jdominguez', NOMBRE_COMPLETO: 'Javier Domínguez', ACTIVO: true },
];

/** Contraseña única del prototipo. No representa ningún mecanismo real de autenticación. */
export const CLAVE_DEMO = 'demo';

export const CLIENTES: FilaClienteCorporativo[] = [
  { CLIENTE_ID: 'CLI-001', CLIENTE_NOMBRE: 'Molinos del Norte S.A.', CLIENTE_CODIGO: 'MOLNOR', ACTIVO: true },
  { CLIENTE_ID: 'CLI-002', CLIENTE_NOMBRE: 'Transporte Andino SRL', CLIENTE_CODIGO: 'TANDINO', ACTIVO: true },
  { CLIENTE_ID: 'CLI-003', CLIENTE_NOMBRE: 'Clínica San Martín', CLIENTE_CODIGO: 'CSMARTIN', ACTIVO: true },
  { CLIENTE_ID: 'CLI-004', CLIENTE_NOMBRE: 'Cooperativa Eléctrica Sur', CLIENTE_CODIGO: 'COOPSUR', ACTIVO: true },
  { CLIENTE_ID: 'CLI-005', CLIENTE_NOMBRE: 'Distribuidora Belgrano', CLIENTE_CODIGO: 'DBELGRA', ACTIVO: true },
  { CLIENTE_ID: 'CLI-006', CLIENTE_NOMBRE: 'Bodega Alto Valle', CLIENTE_CODIGO: 'BALTOV', ACTIVO: true },
];

/** Genera una fecha ISO relativa a hoy, para que la lista siempre luzca reciente. */
function hace(dias: number, hora: number, minuto: number): string {
  const d = new Date();
  d.setDate(d.getDate() - dias);
  d.setHours(hora, minuto, 0, 0);
  return d.toISOString();
}

export const TICKETS: FilaTicketCorporativo[] = [
  {
    TICKET_ID: 'SUP-14892', CLIENTE_ID: 'CLI-001', CLIENTE_NOMBRE: 'Molinos del Norte S.A.',
    TITULO: 'Error al generar remito de salida en depósito 3',
    ESTADO: 'En curso', PRIORIDAD: 'Alta', FECHA_CREACION: hace(0, 8, 12), ASIGNADO_A: 'Cristian Pereyra',
  },
  {
    TICKET_ID: 'SUP-14889', CLIENTE_ID: 'CLI-004', CLIENTE_NOMBRE: 'Cooperativa Eléctrica Sur',
    TITULO: 'Solicitud de alta de usuario para facturación',
    ESTADO: 'Abierto', PRIORIDAD: 'Media', FECHA_CREACION: hace(0, 7, 45), ASIGNADO_A: 'Marina López',
  },
  {
    TICKET_ID: 'SUP-14885', CLIENTE_ID: 'CLI-002', CLIENTE_NOMBRE: 'Transporte Andino SRL',
    TITULO: 'La app de choferes no sincroniza viajes desde ayer',
    ESTADO: 'En curso', PRIORIDAD: 'Alta', FECHA_CREACION: hace(1, 17, 3), ASIGNADO_A: 'Cristian Pereyra',
  },
  {
    TICKET_ID: 'SUP-14881', CLIENTE_ID: 'CLI-003', CLIENTE_NOMBRE: 'Clínica San Martín',
    TITULO: 'Turnos duplicados al reprogramar desde el portal',
    ESTADO: 'Pendiente cliente', PRIORIDAD: 'Media', FECHA_CREACION: hace(1, 14, 20), ASIGNADO_A: 'Javier Domínguez',
  },
  {
    TICKET_ID: 'SUP-14877', CLIENTE_ID: 'CLI-001', CLIENTE_NOMBRE: 'Molinos del Norte S.A.',
    TITULO: 'Reporte mensual de stock arroja totales negativos',
    ESTADO: 'Abierto', PRIORIDAD: 'Alta', FECHA_CREACION: hace(1, 11, 50), ASIGNADO_A: 'Cristian Pereyra',
  },
  {
    TICKET_ID: 'SUP-14870', CLIENTE_ID: 'CLI-005', CLIENTE_NOMBRE: 'Distribuidora Belgrano',
    TITULO: 'Capacitación de uso del módulo de cobranzas',
    ESTADO: 'Abierto', PRIORIDAD: 'Baja', FECHA_CREACION: hace(2, 9, 30), ASIGNADO_A: 'Marina López',
  },
  {
    TICKET_ID: 'SUP-14866', CLIENTE_ID: 'CLI-004', CLIENTE_NOMBRE: 'Cooperativa Eléctrica Sur',
    TITULO: 'Lentitud al consultar histórico de consumos',
    ESTADO: 'En curso', PRIORIDAD: 'Media', FECHA_CREACION: hace(2, 16, 5), ASIGNADO_A: 'Cristian Pereyra',
  },
  {
    TICKET_ID: 'SUP-14858', CLIENTE_ID: 'CLI-002', CLIENTE_NOMBRE: 'Transporte Andino SRL',
    TITULO: 'Ajuste de permisos para perfil supervisor de flota',
    ESTADO: 'Resuelto', PRIORIDAD: 'Baja', FECHA_CREACION: hace(3, 10, 15), ASIGNADO_A: 'Javier Domínguez',
  },
  {
    TICKET_ID: 'SUP-14851', CLIENTE_ID: 'CLI-006', CLIENTE_NOMBRE: 'Bodega Alto Valle',
    TITULO: 'Integración con balanza no registra pesadas parciales',
    ESTADO: 'En curso', PRIORIDAD: 'Alta', FECHA_CREACION: hace(3, 8, 40), ASIGNADO_A: 'Cristian Pereyra',
  },
  {
    TICKET_ID: 'SUP-14845', CLIENTE_ID: 'CLI-003', CLIENTE_NOMBRE: 'Clínica San Martín',
    TITULO: 'Certificado vencido en el servidor de historias clínicas',
    ESTADO: 'Cerrado', PRIORIDAD: 'Alta', FECHA_CREACION: hace(4, 13, 25), ASIGNADO_A: 'Marina López',
  },
  {
    TICKET_ID: 'SUP-14839', CLIENTE_ID: 'CLI-005', CLIENTE_NOMBRE: 'Distribuidora Belgrano',
    TITULO: 'Exportación de cuenta corriente sin columna de saldo',
    ESTADO: 'Abierto', PRIORIDAD: 'Media', FECHA_CREACION: hace(4, 9, 5), ASIGNADO_A: 'Cristian Pereyra',
  },
  {
    TICKET_ID: 'SUP-14830', CLIENTE_ID: 'CLI-001', CLIENTE_NOMBRE: 'Molinos del Norte S.A.',
    TITULO: 'Backup nocturno finaliza con advertencias',
    ESTADO: 'Pendiente cliente', PRIORIDAD: 'Media', FECHA_CREACION: hace(5, 22, 10), ASIGNADO_A: 'Javier Domínguez',
  },
  {
    TICKET_ID: 'SUP-14822', CLIENTE_ID: 'CLI-006', CLIENTE_NOMBRE: 'Bodega Alto Valle',
    TITULO: 'Alta de nueva sucursal en el maestro de depósitos',
    ESTADO: 'Resuelto', PRIORIDAD: 'Baja', FECHA_CREACION: hace(6, 11, 0), ASIGNADO_A: 'Cristian Pereyra',
  },
  {
    TICKET_ID: 'SUP-14814', CLIENTE_ID: 'CLI-002', CLIENTE_NOMBRE: 'Transporte Andino SRL',
    TITULO: 'Impresora fiscal no responde en terminal 2',
    ESTADO: 'Cerrado', PRIORIDAD: 'Alta', FECHA_CREACION: hace(7, 15, 45), ASIGNADO_A: 'Marina López',
  },
  {
    TICKET_ID: 'SUP-14803', CLIENTE_ID: 'CLI-004', CLIENTE_NOMBRE: 'Cooperativa Eléctrica Sur',
    TITULO: 'Revisión de índices en la base de medidores',
    ESTADO: 'En curso', PRIORIDAD: 'Media', FECHA_CREACION: hace(8, 10, 30), ASIGNADO_A: 'Cristian Pereyra',
  },
];
