import {
  duracionMs,
  formatearDuracion,
  formatearHora,
  sesionesOrdenadas,
} from '../domain/resumen';
import type { Jornada } from '../domain/tipos';

interface Segmento {
  clave: string;
  clase: 'Principal' | 'Interrupcion' | 'descanso';
  inicio: number;
  fin: number;
}

/** Construye los tramos visibles de la jornada, incluyendo los descansos como huecos. */
function segmentos(jornada: Jornada, ahora: number): Segmento[] {
  const orden = sesionesOrdenadas(jornada);
  const out: Segmento[] = [];

  orden.forEach((s, i) => {
    const previa = orden[i - 1];
    if (previa && previa.fin !== null && s.inicio > previa.fin) {
      out.push({
        clave: `hueco-${s.id}`,
        clase: 'descanso',
        inicio: previa.fin,
        fin: s.inicio,
      });
    }
    out.push({
      clave: s.id,
      clase: s.tipo,
      inicio: s.inicio,
      fin: s.fin ?? ahora,
    });
  });

  return out;
}

export function LineaTemporal({ jornada, ahora }: { jornada: Jornada; ahora: number }) {
  const orden = sesionesOrdenadas(jornada);
  const tramos = segmentos(jornada, ahora);
  const total = tramos.reduce((acc, t) => acc + (t.fin - t.inicio), 0) || 1;

  return (
    <>
      <div
        className="banda"
        role="img"
        aria-label={`Distribución de la jornada en ${tramos.length} tramos`}
      >
        {tramos.map((t) => (
          <div
            key={t.clave}
            className={`banda__tramo banda__tramo--${t.clase}`}
            style={{ flexGrow: (t.fin - t.inicio) / total }}
            title={`${formatearHora(t.inicio)} – ${formatearHora(t.fin)}`}
          />
        ))}
      </div>

      <div className="banda__leyenda">
        <span className="leyenda-item">
          <span className="leyenda-item__muestra" style={{ background: 'var(--activa)' }} />
          Tarea principal
        </span>
        <span className="leyenda-item">
          <span className="leyenda-item__muestra" style={{ background: 'var(--finalizada)' }} />
          Interrupción
        </span>
        <span className="leyenda-item">
          <span
            className="leyenda-item__muestra"
            style={{ background: 'var(--descanso-fondo)', border: '1px solid var(--descanso)' }}
          />
          Descanso
        </span>
      </div>

      <div className="linea">
        {orden.map((s, i) => {
          const previa = orden[i - 1];
          const hayDescanso =
            previa && previa.fin !== null && s.inicio > previa.fin;

          return (
            <div key={s.id}>
              {hayDescanso && (
                <div className="tramo tramo--descanso">
                  <span className="tramo__hora mono">
                    {formatearHora(previa.fin as number)}
                  </span>
                  <span className="tramo__detalle">
                    <span className="tramo__titulo">Descanso — sin tiempo imputado</span>
                  </span>
                  <span className="tramo__dur mono">
                    {formatearDuracion(s.inicio - (previa.fin as number))}
                  </span>
                </div>
              )}

              <div
                className={`tramo tramo--${s.tipo}${s.fin === null ? ' tramo--abierto' : ''}`}
              >
                <span className="tramo__hora mono">
                  {formatearHora(s.inicio)}
                  {s.fin !== null ? `–${formatearHora(s.fin)}` : '–··· '}
                </span>
                <span className="tramo__detalle">
                  <span className="tramo__ticket">{s.ticket.id}</span>{' '}
                  <span className="tramo__titulo">{s.ticket.titulo}</span>
                </span>
                <span className="tramo__dur mono">
                  {formatearDuracion(duracionMs(s, ahora))}
                </span>
              </div>
            </div>
          );
        })}
      </div>
    </>
  );
}
