import { LineaTemporal } from '../componentes/LineaTemporal';
import { Pastilla } from '../componentes/Pastilla';
import { ETIQUETA_ACCION, accionesHabilitadas, sesionAbierta } from '../domain/maquinaEstados';
import {
  duracionMs,
  formatearDuracion,
  formatearFechaLarga,
  formatearHora,
  totalTrabajado,
} from '../domain/resumen';
import type { Jornada, TipoAccion } from '../domain/tipos';

interface Props {
  jornada: Jornada | null;
  ahora: number;
  enviando: boolean;
  onAccion: (accion: TipoAccion) => void;
  onRevisar: () => void;
  onGenerarExcel: () => void;
  onReabrir: () => void;
}

/**
 * Panel principal (§15.1): funciona como tablero de estado, no como formulario.
 * Abajo aparecen únicamente las acciones válidas para el estado actual.
 */
export function PantallaPanel({
  jornada,
  ahora,
  enviando,
  onAccion,
  onRevisar,
  onGenerarExcel,
  onReabrir,
}: Props) {
  const estado = jornada?.estado ?? 'Pendiente';
  const habilitadas = accionesHabilitadas(estado);
  const abierta = jornada ? sesionAbierta(jornada) : undefined;
  const corriendo = estado === 'Activa' && abierta;

  return (
    <main className="contenido">
      <section className="tarjeta">
        <header className="tarjeta__cabecera">
          <Pastilla estado={estado} />
          {jornada && (
            <span className="etiqueta">
              Inicio {formatearHora(jornada.inicio)}
              {jornada.fin !== null && ` · Cierre ${formatearHora(jornada.fin)}`}
            </span>
          )}
        </header>

        <div className="estado">
          {!jornada && (
            <div className="estado__vacio">
              <span className="estado__vacio-titulo">Todavía no comenzaste el día</span>
              <span>
                Al comenzar vas a elegir el ticket de tu primera tarea principal. Desde ahí,
                el asistente registra cada corte y reanudación por vos.
              </span>
            </div>
          )}

          {jornada && estado === 'EnDescanso' && (
            <div className="estado__vacio">
              <span className="estado__vacio-titulo">En descanso</span>
              <span>
                No se está imputando tiempo. Al regresar se reanuda{' '}
                <strong className="mono">{jornada.ticketPrincipal?.id}</strong>.
              </span>
            </div>
          )}

          {jornada && estado === 'Finalizada' && (
            <div className="estado__vacio">
              <span className="estado__vacio-titulo">Día finalizado</span>
              <span>
                La jornada del {formatearFechaLarga(jornada.inicio)} está cerrada. Podés
                revisarla y generar el Excel.
              </span>
            </div>
          )}

          {jornada && corriendo && abierta && (
            <div className="apilar" style={{ alignItems: 'center' }}>
              <span className="etiqueta">Tarea principal</span>
              <span className="estado__ticket-id">{abierta.ticket.id}</span>
              <span className="estado__titulo">{abierta.ticket.titulo}</span>
              <span className="estado__cliente">{abierta.ticket.clienteNombre}</span>
            </div>
          )}

          {jornada && (
            <div className="apilar" style={{ alignItems: 'center' }}>
              <span className={corriendo ? 'crono' : 'crono crono--quieto'}>
                {formatearDuracion(
                  abierta && corriendo ? duracionMs(abierta, ahora) : 0,
                  true,
                )}
              </span>
              <div className="crono__pie">
                <span className="metrica">
                  <span className="metrica__valor">
                    {formatearDuracion(totalTrabajado(jornada, ahora))}
                  </span>
                  <span className="etiqueta">Acumulado del día</span>
                </span>
                <span className="metrica">
                  <span className="metrica__valor">{jornada.sesiones.length}</span>
                  <span className="etiqueta">Tramos</span>
                </span>
                {abierta && corriendo && (
                  <span className="metrica">
                    <span className="metrica__valor">{formatearHora(abierta.inicio)}</span>
                    <span className="etiqueta">Último inicio</span>
                  </span>
                )}
              </div>
            </div>
          )}
        </div>

        <div
          className={habilitadas.length === 1 ? 'acciones acciones--una' : 'acciones'}
        >
          {habilitadas.map((a) => (
            <button
              key={a}
              className={botonClase(a, habilitadas.length)}
              disabled={enviando}
              onClick={() => onAccion(a)}
            >
              {ETIQUETA_ACCION[a]}
            </button>
          ))}

          {habilitadas.length === 0 && (
            <>
              <button className="btn btn--grande" onClick={onRevisar}>
                Revisar jornada
              </button>
              <button
                className="btn btn--principal btn--grande"
                onClick={onGenerarExcel}
                disabled={jornada === null || jornada.sesiones.length === 0}
              >
                Generar Excel
              </button>
              {/* Corrección, no parte del flujo normal: se ofrece sin competir con los
                  dos botones principales (§6, "salvo corrección autorizada"). */}
              <button className="btn btn--sutil acciones__correccion" onClick={onReabrir}>
                Reabrir jornada
              </button>
            </>
          )}

          {enviando && <span className="acciones__nota">Registrando…</span>}
        </div>
      </section>

      <section className="tarjeta">
        <header className="tarjeta__cabecera">
          <h2 className="tarjeta__titulo">Línea temporal</h2>
          {jornada && (
            <button className="btn btn--sutil" onClick={onRevisar}>
              Revisar →
            </button>
          )}
        </header>
        <div className="tarjeta__cuerpo">
          {jornada ? (
            <LineaTemporal jornada={jornada} ahora={ahora} />
          ) : (
            <div className="bloque">
              <span className="bloque__titulo">Sin tramos registrados</span>
              <span>Los tramos aparecen a medida que registrás tu jornada.</span>
            </div>
          )}
        </div>
      </section>
    </main>
  );
}

function botonClase(accion: TipoAccion, cantidad: number): string {
  const grande = cantidad <= 2 ? ' btn--grande' : '';
  if (accion === 'ComenzarDia' || accion === 'FinTarea' || accion === 'RegresoDescanso') {
    return `btn btn--principal${grande}`;
  }
  if (accion === 'FinDia') return `btn btn--peligro${grande}`;
  return `btn${grande}`;
}
