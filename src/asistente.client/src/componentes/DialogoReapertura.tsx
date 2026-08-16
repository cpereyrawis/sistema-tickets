import { useState } from 'react';
import { Modal } from './Modal';
import { formatearDuracion, formatearHora } from '../domain/resumen';
import type { Jornada } from '../domain/tipos';

interface Props {
  jornada: Jornada;
  ahora: number;
  /** Cantidad de exportaciones ya emitidas para esta jornada. */
  exportaciones: number;
  enviando: boolean;
  onConfirmar: (motivo: string, imputarIntervalo: boolean) => void;
  onCancelar: () => void;
}

/** A partir de acá, imputar el intervalo completo merece una advertencia extra. */
const INTERVALO_LARGO_MS = 60 * 60_000;

/**
 * Reapertura de una jornada cerrada por error.
 *
 * La especificación permite operar sobre una jornada cerrada "salvo corrección
 * autorizada" (§6) y exige que la corrección quede auditada (FR-035, NFR-007).
 *
 * El usuario elige si el intervalo entre el cierre y la reapertura se imputa a la tarea
 * principal o queda como hueco. La opción por defecto es la conservadora —no imputar—
 * para que el tiempo trabajado nunca crezca por inercia: computarlo es una decisión
 * deliberada, no lo que pasa si nadie lee el diálogo.
 */
export function DialogoReapertura({
  jornada,
  ahora,
  exportaciones,
  enviando,
  onConfirmar,
  onCancelar,
}: Props) {
  const [motivo, setMotivo] = useState('Cierre por error');
  const [imputar, setImputar] = useState(false);
  const limpio = motivo.trim();

  const cerradaEn = jornada.fin;
  const intervalo = cerradaEn !== null ? ahora - cerradaEn : 0;
  const ticket = jornada.ticketPrincipal?.id ?? 'la tarea principal';
  const intervaloLargo = intervalo >= INTERVALO_LARGO_MS;

  return (
    <Modal
      titulo="Reabrir la jornada"
      contexto="Queda registrado como corrección auditada"
      angosto
      onCerrar={onCancelar}
      pie={
        <>
          <button className="btn" onClick={onCancelar}>
            Cancelar
          </button>
          <button
            className="btn btn--principal"
            disabled={limpio === '' || enviando}
            onClick={() => onConfirmar(limpio, imputar)}
          >
            Reabrir jornada
          </button>
        </>
      }
    >
      <p style={{ margin: 0, color: 'var(--texto-2)' }}>
        La jornada vuelve al estado activo y se reanuda{' '}
        <strong className="mono" style={{ color: 'var(--texto)' }}>
          {ticket}
        </strong>
        {cerradaEn !== null && (
          <>
            . Cerraste a las <strong className="mono">{formatearHora(cerradaEn)}</strong>, hace{' '}
            <strong className="mono">{formatearDuracion(intervalo)}</strong>
          </>
        )}
        .
      </p>

      {cerradaEn !== null && (
        <fieldset className="opciones">
          <legend className="campo__etiqueta">¿Qué hacemos con ese intervalo?</legend>

          <label className={imputar ? 'opcion' : 'opcion opcion--elegida'}>
            <input
              type="radio"
              name="imputar"
              checked={!imputar}
              onChange={() => setImputar(false)}
            />
            <span>
              <span className="opcion__titulo">No imputarlo</span>
              <span className="opcion__ayuda">
                Queda como hueco, igual que un descanso. El tramo nuevo empieza ahora.
              </span>
            </span>
          </label>

          <label className={imputar ? 'opcion opcion--elegida' : 'opcion'}>
            <input
              type="radio"
              name="imputar"
              checked={imputar}
              onChange={() => setImputar(true)}
            />
            <span>
              <span className="opcion__titulo">
                Imputar <span className="mono">{formatearDuracion(intervalo)}</span> a{' '}
                <span className="mono">{ticket}</span>
              </span>
              <span className="opcion__ayuda">
                El tramo nuevo empieza a las {formatearHora(cerradaEn)}, cuando cerraste, y ese
                tiempo cuenta como trabajado.
              </span>
            </span>
          </label>
        </fieldset>
      )}

      {imputar && intervaloLargo && (
        <div className="aviso aviso--alerta">
          Vas a sumar <strong className="mono">{formatearDuracion(intervalo)}</strong> a{' '}
          {ticket}. Es un intervalo largo: verificá que lo hayas trabajado, y que no incluya
          un descanso.
        </div>
      )}

      {exportaciones > 0 && (
        <div className="aviso aviso--alerta">
          Esta jornada ya se exportó {exportaciones === 1 ? 'una vez' : `${exportaciones} veces`}.
          Si la modificás, tenés que volver a generar el Excel; la nueva copia queda
          identificada como regeneración.
        </div>
      )}

      <div className="campo">
        <label className="campo__etiqueta" htmlFor="r-motivo">
          Motivo de la corrección
        </label>
        <input
          id="r-motivo"
          className="entrada"
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
          maxLength={120}
        />
        <span className="campo__ayuda">
          Se guarda junto con tu usuario y la hora, y aparece en la revisión de la jornada.
        </span>
      </div>
    </Modal>
  );
}
