/**
 * Jornada de ejemplo para demostrar el prototipo.
 *
 * No se construye a mano: se arma aplicando la propia máquina de estados con marcas
 * temporales explícitas. Así el ejemplo cumple los mismos invariantes que una jornada
 * real y no puede quedar inconsistente con el dominio.
 *
 * Todos los instantes son relativos a "ahora", de modo que el ejemplo siempre queda
 * en el pasado sin importar a qué hora se abra el prototipo.
 */

import { aplicar } from '../domain/maquinaEstados';
import type { Jornada, TicketRef, Usuario } from '../domain/tipos';
import { TICKETS } from './datos';

function ref(ticketId: string): TicketRef {
  const fila = TICKETS.find((t) => t.TICKET_ID === ticketId);
  if (!fila) throw new Error(`Ticket de ejemplo inexistente: ${ticketId}`);
  return {
    id: fila.TICKET_ID,
    clienteId: fila.CLIENTE_ID,
    clienteNombre: fila.CLIENTE_NOMBRE,
    titulo: fila.TITULO,
  };
}

const MIN = 60_000;

export function construirJornadaEjemplo(usuario: Usuario, ahora: number): Jornada {
  const t0 = ahora - 300 * MIN; // hace 5 horas

  let j: Jornada | null = null;

  const paso = (accion: Parameters<typeof aplicar>[1]) => {
    const r = aplicar(j, accion, usuario);
    if (!r.ok) throw new Error(`Jornada de ejemplo inválida: ${r.mensaje}`);
    j = r.valor;
  };

  // Comienza el día con la incidencia de Molinos del Norte.
  paso({ tipo: 'ComenzarDia', ticket: ref('SUP-14892'), ahora: t0 });

  // A los 80 minutos pasa al problema de sincronización de Transporte Andino.
  paso({ tipo: 'FinTarea', ticket: ref('SUP-14885'), ahora: t0 + 80 * MIN });

  // Una consulta breve de Cooperativa Eléctrica interrumpe 20 minutos y luego
  // se reanuda automáticamente la misma tarea principal.
  paso({
    tipo: 'RegistrarInterrupcion',
    ticket: ref('SUP-14889'),
    inicio: t0 + 120 * MIN,
    duracionMinutos: 20,
    ahora: t0 + 145 * MIN,
  });

  // Sale a almorzar y regresa 35 minutos después.
  paso({ tipo: 'SalidaDescanso', ahora: t0 + 190 * MIN });
  paso({ tipo: 'RegresoDescanso', ahora: t0 + 225 * MIN });

  // Queda activa hasta ahora, con el cronómetro corriendo.
  return j as unknown as Jornada;
}
