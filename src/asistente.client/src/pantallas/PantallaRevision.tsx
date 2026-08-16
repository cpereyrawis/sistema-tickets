import { LineaTemporal } from '../componentes/LineaTemporal';
import {
  agruparPorTicket,
  detectarAnomalias,
  filasExportacion,
  formatearDuracion,
  formatearFechaLarga,
  formatearHora,
  nombreArchivoExcel,
  totalTrabajado,
} from '../domain/resumen';
import type { Jornada, Usuario } from '../domain/tipos';

interface Props {
  jornada: Jornada;
  usuario: Usuario;
  ahora: number;
  onVolver: () => void;
  onGenerarExcel: () => void;
  /** Cantidad de veces que ya se exportó esta jornada, para identificar regeneraciones. */
  exportaciones: number;
}

/**
 * Revisión de la jornada y previsualización del Excel (§14, FR-040 a FR-045).
 *
 * El prototipo NO genera el archivo: muestra exactamente las filas que se escribirían.
 * El mapeo definitivo depende de la plantilla corporativa, que todavía no se relevó.
 */
export function PantallaRevision({
  jornada,
  usuario,
  ahora,
  onVolver,
  onGenerarExcel,
  exportaciones,
}: Props) {
  const grupos = agruparPorTicket(jornada, ahora);
  const anomalias = detectarAnomalias(jornada);
  const filas = filasExportacion(jornada);
  const total = totalTrabajado(jornada, ahora);
  const interrupciones = jornada.sesiones.filter((s) => s.tipo === 'Interrupcion').length;

  return (
    <main className="contenido contenido--unica">
      <div className="fila" style={{ gap: 'var(--e-3)' }}>
        <button className="btn btn--sutil" onClick={onVolver}>
          ← Volver al panel
        </button>
        <h1 style={{ fontSize: 'var(--t-lg)', marginLeft: 'var(--e-2)' }}>
          Revisión de la jornada
        </h1>
      </div>

      <section className="tarjeta">
        <header className="tarjeta__cabecera">
          <h2 className="tarjeta__titulo">
            {formatearFechaLarga(jornada.inicio)}
          </h2>
          <span className="etiqueta">{jornada.eventos.length} eventos registrados</span>
        </header>
        <div className="tarjeta__cuerpo">
          <div className="resumen-export">
            <div className="dato">
              <span className="etiqueta">Total registrado</span>
              <span className="dato__valor">{formatearDuracion(total)}</span>
            </div>
            <div className="dato">
              <span className="etiqueta">Tramos</span>
              <span className="dato__valor">{jornada.sesiones.length}</span>
            </div>
            <div className="dato">
              <span className="etiqueta">Interrupciones</span>
              <span className="dato__valor">{interrupciones}</span>
            </div>
            <div className="dato">
              <span className="etiqueta">Tickets tocados</span>
              <span className="dato__valor">{grupos.length}</span>
            </div>
          </div>

          {jornada.auditoria.length > 0 && (
            <div className="apilar" style={{ gap: 'var(--e-2)' }}>
              <span className="etiqueta">Correcciones registradas</span>
              {jornada.auditoria.map((a) => (
                <div key={a.id} className="aviso aviso--alerta">
                  <div>
                    <strong>{a.accion}</strong> · {formatearHora(a.ocurridoEn)} ·{' '}
                    {usuario.usuario}
                    <div style={{ marginTop: 3 }}>{a.detalle}</div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {anomalias.length === 0 ? (
            <div className="aviso aviso--info">
              Sin huecos ni solapamientos. Los descansos y las reaperturas de jornada no se
              cuentan como anomalía.
            </div>
          ) : (
            <div className="apilar" style={{ gap: 'var(--e-2)' }}>
              {anomalias.map((a, i) => (
                <div key={i} className="aviso aviso--alerta">
                  {a.mensaje}
                </div>
              ))}
            </div>
          )}

          <LineaTemporal jornada={jornada} ahora={ahora} />
        </div>
      </section>

      <section className="tarjeta">
        <header className="tarjeta__cabecera">
          <h2 className="tarjeta__titulo">Agrupado por ticket</h2>
        </header>
        <div className="tarjeta__cuerpo">
          <div className="tabla-envoltorio">
            <table className="tabla">
              <thead>
                <tr>
                  <th>Ticket</th>
                  <th>Cliente</th>
                  <th>Título</th>
                  <th>Tramos</th>
                  <th>Total</th>
                </tr>
              </thead>
              <tbody>
                {grupos.map((g) => (
                  <tr key={g.ticketId}>
                    <td className="num">{g.ticketId}</td>
                    <td>{g.clienteNombre}</td>
                    <td className="titulo-celda">{g.titulo}</td>
                    <td className="num">{g.tramos}</td>
                    <td className="num">{formatearDuracion(g.totalMs)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <section className="tarjeta">
        <header className="tarjeta__cabecera">
          <h2 className="tarjeta__titulo">Previsualización del Excel</h2>
          <span className="etiqueta">{filas.length} filas</span>
        </header>
        <div className="tarjeta__cuerpo">
          <div className="aviso aviso--alerta">
            Perfil de columnas provisional. El mapeo definitivo depende de la plantilla
            corporativa, que todavía no se relevó (decisión D-7 del plan de implementación).
          </div>

          <div className="tabla-envoltorio">
            <table className="tabla">
              <thead>
                <tr>
                  <th>Fecha</th>
                  <th>Ticket</th>
                  <th>Cliente</th>
                  <th>Inicio</th>
                  <th>Fin</th>
                  <th>Duración</th>
                  <th>Tipo</th>
                  <th>Motivo</th>
                </tr>
              </thead>
              <tbody>
                {filas.map((f, i) => (
                  <tr key={i}>
                    <td className="num">{f.fecha}</td>
                    <td className="num">{f.ticket}</td>
                    <td>{f.cliente}</td>
                    <td className="num">{f.inicio}</td>
                    <td className="num">{f.fin}</td>
                    <td className="num">{f.duracion}</td>
                    <td>{f.tipo}</td>
                    <td className="titulo-celda">{f.motivo}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="fila" style={{ gap: 'var(--e-3)', flexWrap: 'wrap' }}>
            <button
              className="btn btn--principal"
              disabled={jornada.estado !== 'Finalizada' || filas.length === 0}
              onClick={onGenerarExcel}
            >
              Generar Excel
            </button>
            <span className="campo__ayuda">
              {jornada.estado !== 'Finalizada'
                ? 'Disponible cuando la jornada esté finalizada.'
                : `Se descarga como ${nombreArchivoExcel(jornada, usuario.usuario)}`}
            </span>
          </div>

          {exportaciones > 0 && (
            <div className="aviso aviso--info">
              <div>
                <strong>
                  {exportaciones === 1
                    ? 'Exportación registrada'
                    : `Exportación registrada — regeneración #${exportaciones - 1}`}
                </strong>
                <div style={{ marginTop: 4 }}>
                  Archivo <code className="mono">{nombreArchivoExcel(jornada, usuario.usuario)}</code>{' '}
                  · usuario <code className="mono">{usuario.usuario}</code>
                </div>
                <div style={{ marginTop: 4, color: 'var(--texto-3)' }}>
                  El archivo usa el perfil de columnas provisional, no la plantilla
                  corporativa. Cuando exista la plantilla real, la generación pasa al
                  backend con ClosedXML.
                </div>
              </div>
            </div>
          )}
        </div>
      </section>
    </main>
  );
}
