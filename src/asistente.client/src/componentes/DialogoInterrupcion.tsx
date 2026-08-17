import { useState } from 'react';
import { Modal } from './Modal';
import { sesionAbierta, validarInterrupcion } from '../domain/maquinaEstados';
import { formatearHora } from '../domain/resumen';
import type { Jornada, TicketRef } from '../domain/tipos';

interface Props {
  jornada: Jornada;
  ticket: TicketRef;
  ahora: number;
  onConfirmar: (inicio: number, duracionMinutos: number) => void;
  onCancelar: () => void;
}

function aHoraLocal(instante: number): string {
  const d = new Date(instante);
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
}

function desdeHoraLocal(hhmm: string, referencia: number): number | null {
  const m = /^(\d{1,2}):(\d{2})$/.exec(hhmm.trim());
  if (!m) return null;
  const h = Number(m[1]);
  const min = Number(m[2]);
  if (h > 23 || min > 59) return null;
  const d = new Date(referencia);
  d.setHours(h, min, 0, 0);
  return d.getTime();
}

function inicioSugerido(jornada: Jornada, ahora: number): number {
  const limite = sesionAbierta(jornada)?.inicio ?? jornada.inicio ?? ahora;
  return Math.max(limite, ahora - 15 * 60_000);
}

function duracionSugerida(jornada: Jornada, ahora: number): number {
  const disponibles = Math.floor((ahora - inicioSugerido(jornada, ahora)) / 60_000);
  return Math.max(1, Math.min(15, disponibles));
}

/**
 * Diálogo de interrupción (§7.3).
 * El usuario informa hora de inicio y duración; la hora de fin se calcula y se muestra
 * ANTES de confirmar (§15.3). La validación replica las seis reglas del dominio.
 */
export function DialogoInterrupcion({
  jornada,
  ticket,
  ahora,
  onConfirmar,
  onCancelar,
}: Props) {
  // El valor propuesto nunca puede caer antes del tramo que se va a cortar: se sugiere
  // el más tardío entre "hace 15 minutos" y el inicio de la sesión vigente, y la duración
  // se acota al tiempo realmente disponible. Así el diálogo no abre en estado inválido.
  const [horaInicio, setHoraInicio] = useState(() => aHoraLocal(inicioSugerido(jornada, ahora)));
  const [duracion, setDuracion] = useState(() =>
    String(duracionSugerida(jornada, ahora)),
  );

  const inicio = desdeHoraLocal(horaInicio, ahora);
  const minutos = Number(duracion);
  const duracionValida = Number.isFinite(minutos) && minutos > 0;
  const fin = inicio !== null && duracionValida ? inicio + minutos * 60_000 : null;

  let problema: string | null = null;
  if (inicio === null) problema = 'Ingresá la hora de inicio con el formato HH:MM.';
  else if (!duracionValida) problema = 'La duración debe ser un número de minutos mayor a cero.';
  else problema = validarInterrupcion(jornada, inicio, minutos, ahora);

  return (
    <Modal
      titulo="Registrar interrupción"
      contexto={`Corta y reanuda automáticamente ${jornada.ticketPrincipal?.id ?? 'la tarea principal'}`}
      angosto
      onCerrar={onCancelar}
      pie={
        <>
          <button className="btn" onClick={onCancelar}>
            Cancelar
          </button>
          <button
            className="btn btn--principal"
            disabled={problema !== null || inicio === null}
            onClick={() => inicio !== null && onConfirmar(inicio, minutos)}
          >
            Confirmar interrupción
          </button>
        </>
      }
    >
      <div className="aviso aviso--info">
        <div>
          <strong className="mono">{ticket.id}</strong>
          <div style={{ marginTop: 2 }}>{ticket.titulo}</div>
          <div style={{ color: 'var(--texto-3)', fontSize: 'var(--t-xs)', marginTop: 2 }}>
            {ticket.clienteNombre}
          </div>
        </div>
      </div>

      <div className="grilla-2">
        <div className="campo">
          <label className="campo__etiqueta" htmlFor="i-hora">
            Hora de inicio
          </label>
          <input
            id="i-hora"
            className="entrada entrada--mono"
            value={horaInicio}
            onChange={(e) => setHoraInicio(e.target.value)}
            placeholder="HH:MM"
            inputMode="numeric"
          />
        </div>

        <div className="campo">
          <label className="campo__etiqueta" htmlFor="i-dur">
            Duración (minutos)
          </label>
          <input
            id="i-dur"
            className="entrada entrada--mono"
            value={duracion}
            onChange={(e) => setDuracion(e.target.value)}
            inputMode="numeric"
          />
        </div>
      </div>

      {fin !== null && !problema && (
        <div className="aviso aviso--calculo">
          Termina a las {formatearHora(fin)}
        </div>
      )}

      {problema && <div className="aviso aviso--error">{problema}</div>}

      <p className="campo__ayuda">
        Al confirmar se generan cuatro eventos en una sola transacción: fin de la tarea
        principal, inicio y fin de la interrupción, y nuevo inicio de la misma tarea principal.
      </p>
    </Modal>
  );
}
