import { useCallback, useEffect, useState } from 'react';
import { DialogoInterrupcion } from './componentes/DialogoInterrupcion';
import { DialogoReapertura } from './componentes/DialogoReapertura';
import { DialogoTickets } from './componentes/DialogoTickets';
import { Modal } from './componentes/Modal';
import { ETIQUETA_ACCION, aplicar } from './domain/maquinaEstados';
import {
  filasExportacion,
  formatearFechaLarga,
  nombreArchivoExcel,
} from './domain/resumen';
import { descargar, generarXlsx } from './services/exportadorExcel';
import type { Accion, Jornada, TicketRef, TipoAccion, Usuario } from './domain/tipos';
import { PantallaLogin } from './pantallas/PantallaLogin';
import { PantallaPanel } from './pantallas/PantallaPanel';
import { PantallaRevision } from './pantallas/PantallaRevision';
import { fallaActiva, simularFalla } from './services/ticketQueryService';
import { almacen } from './state/almacenamiento';
import { ESQUEMA_SIMULADO } from './mock/esquema';
import { construirJornadaEjemplo } from './mock/jornadaEjemplo';

/** Diálogo abierto en este momento. */
type Flujo =
  | { tipo: 'ninguno' }
  /** `marca` es la marca temporal candidata capturada al pulsar el botón (§7.2). */
  | { tipo: 'buscarTicket'; motivo: 'ComenzarDia' | 'FinTarea' | 'Interrupcion'; marca: number }
  | { tipo: 'interrupcionDatos'; ticket: TicketRef }
  | { tipo: 'confirmarFinEnDescanso' }
  | { tipo: 'reabrir' }
  | { tipo: 'esquema' };

type Tema = 'claro' | 'oscuro' | 'sistema';

export default function App() {
  const [usuario, setUsuario] = useState<Usuario | null>(() => almacen.leerUsuario());
  const [jornada, setJornada] = useState<Jornada | null>(() => almacen.leerJornada());
  const [flujo, setFlujo] = useState<Flujo>({ tipo: 'ninguno' });
  const [vista, setVista] = useState<'panel' | 'revision'>('panel');
  const [error, setError] = useState<string | null>(null);
  const [nota, setNota] = useState<string | null>(null);
  const [exportaciones, setExportaciones] = useState(0);
  const [enviando, setEnviando] = useState(false);
  const [ahora, setAhora] = useState(() => Date.now());
  const [tema, setTema] = useState<Tema>(() => almacen.leerTema());
  const [falla, setFalla] = useState(() => fallaActiva());

  // Reloj del cronómetro. El tiempo transcurrido se calcula siempre desde la hora de
  // inicio guardada, nunca acumulando en el cliente: así recargar no pierde precisión.
  useEffect(() => {
    const id = setInterval(() => setAhora(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    if (tema === 'sistema') document.documentElement.removeAttribute('data-theme');
    else document.documentElement.setAttribute('data-theme', tema === 'oscuro' ? 'dark' : 'light');
    almacen.guardarTema(tema);
  }, [tema]);

  useEffect(() => {
    almacen.guardarUsuario(usuario);
  }, [usuario]);

  useEffect(() => {
    almacen.guardarJornada(jornada);
  }, [jornada]);

  /**
   * Aplica una acción del dominio.
   * `enviando` bloquea el doble envío mientras la operación está en curso (§15.3);
   * la demora simula el viaje al backend que hará el sistema real.
   */
  const ejecutar = useCallback(
    (accion: Accion) => {
      if (!usuario || enviando) return;
      setEnviando(true);
      setError(null);

      setTimeout(() => {
        const r = aplicar(jornada, accion, usuario);
        if (r.ok) {
          setJornada(r.valor);
          setFlujo({ tipo: 'ninguno' });
        } else if (r.codigo === 'CONFIRMACION_REQUERIDA') {
          setFlujo({ tipo: 'confirmarFinEnDescanso' });
        } else {
          setError(r.mensaje);
          setFlujo({ tipo: 'ninguno' });
        }
        setEnviando(false);
      }, 180);
    },
    [jornada, usuario, enviando],
  );

  /**
   * Genera y descarga el .xlsx sin pasar por la revisión.
   * Registra la corrida para que las regeneraciones queden identificadas (FR-045).
   */
  const generarExcel = useCallback(() => {
    if (!jornada || !usuario) return;
    const filas = filasExportacion(jornada);
    if (filas.length === 0) {
      setError('La jornada no tiene tramos cerrados para exportar.');
      return;
    }

    const nombre = nombreArchivoExcel(jornada, usuario.usuario);
    descargar(generarXlsx(filas), nombre);
    setExportaciones((n) => n + 1);
    setError(null);

    const cuenta = `${filas.length} ${filas.length === 1 ? 'fila' : 'filas'}`;
    setNota(
      exportaciones === 0
        ? `Excel generado: ${nombre} · ${cuenta}.`
        : `Excel regenerado (#${exportaciones}): ${nombre} · ${cuenta}.`,
    );
  }, [jornada, usuario, exportaciones]);

  /** Traduce el botón pulsado en un diálogo o en una acción directa. */
  function alPulsarAccion(accion: TipoAccion) {
    setError(null);
    setNota(null);
    const marca = Date.now();

    switch (accion) {
      case 'ComenzarDia':
      case 'FinTarea':
        // La marca se captura ahora y solo se confirma al elegir ticket (§7.2, AC-04).
        setFlujo({ tipo: 'buscarTicket', motivo: accion, marca });
        break;
      case 'RegistrarInterrupcion':
        setFlujo({ tipo: 'buscarTicket', motivo: 'Interrupcion', marca });
        break;
      case 'SalidaDescanso':
        ejecutar({ tipo: 'SalidaDescanso', ahora: marca });
        break;
      case 'RegresoDescanso':
        ejecutar({ tipo: 'RegresoDescanso', ahora: marca });
        break;
      case 'FinDia':
        ejecutar({ tipo: 'FinDia', ahora: marca });
        break;
    }
  }

  function alElegirTicket(ticket: TicketRef) {
    if (flujo.tipo !== 'buscarTicket') return;

    if (flujo.motivo === 'Interrupcion') {
      setFlujo({ tipo: 'interrupcionDatos', ticket });
      return;
    }
    ejecutar(
      flujo.motivo === 'ComenzarDia'
        ? { tipo: 'ComenzarDia', ticket, ahora: flujo.marca }
        : { tipo: 'FinTarea', ticket, ahora: flujo.marca },
    );
  }

  function cerrarSesion() {
    setUsuario(null);
    setJornada(null);
    setVista('panel');
    setFlujo({ tipo: 'ninguno' });
  }

  if (!usuario) {
    return <PantallaLogin onEntrar={setUsuario} />;
  }

  const iniciales = usuario.nombre
    .split(' ')
    .slice(0, 2)
    .map((p) => p[0])
    .join('');

  return (
    <div className="app">
      <header className="barra">
        <div className="barra__marca">
          <span className="barra__nombre">Asistente de Registro</span>
          <span className="barra__jornada">
            {jornada
              ? formatearFechaLarga(jornada.inicio)
              : 'Sin jornada abierta'}
          </span>
        </div>

        <div className="barra__usuario">
          <span className="avatar" aria-hidden="true">
            {iniciales}
          </span>
          <span>{usuario.nombre}</span>
        </div>

        <button className="btn btn--sutil" onClick={cerrarSesion}>
          Cerrar sesión
        </button>
      </header>

      {(error || nota) && (
        <div style={{ padding: '0 var(--e-5)', marginTop: 'var(--e-4)' }}>
          <div
            className={error ? 'aviso aviso--error' : 'aviso aviso--info'}
            style={{ maxWidth: 1180, marginInline: 'auto' }}
            role="alert"
          >
            {error ?? nota}
          </div>
        </div>
      )}

      {vista === 'panel' || !jornada ? (
        <PantallaPanel
          jornada={jornada}
          ahora={ahora}
          enviando={enviando}
          onAccion={alPulsarAccion}
          onRevisar={() => setVista('revision')}
          onGenerarExcel={generarExcel}
          onReabrir={() => {
            setError(null);
            setNota(null);
            setFlujo({ tipo: 'reabrir' });
          }}
        />
      ) : (
        <PantallaRevision
          jornada={jornada}
          usuario={usuario}
          ahora={ahora}
          onVolver={() => setVista('panel')}
          onGenerarExcel={generarExcel}
          exportaciones={exportaciones}
        />
      )}

      <footer className="pie">
        <span>
          Prototipo visual · datos simulados en memoria · sin consultas a base de datos
        </span>
        <button className="btn btn--sutil" onClick={() => setFlujo({ tipo: 'esquema' })}>
          Ver esquema simulado
        </button>
        <button
          className="btn btn--sutil"
          onClick={() => {
            setError(null);
            setNota(null);
            setExportaciones(0);
            setJornada(construirJornadaEjemplo(usuario, Date.now()));
            setVista('panel');
          }}
        >
          Cargar jornada de ejemplo
        </button>
        <button
          className="btn btn--sutil"
          disabled={!jornada}
          onClick={() => {
            setError(null);
            setNota(null);
            setExportaciones(0);
            setJornada(null);
            setVista('panel');
          }}
        >
          Reiniciar jornada
        </button>

        <label className="interruptor pie__sep">
          <input
            type="checkbox"
            checked={falla}
            onChange={(e) => {
              setFalla(e.target.checked);
              simularFalla(e.target.checked);
            }}
          />
          Simular caída de la fuente de tickets
        </label>

        <label className="interruptor">
          Tema
          <select
            className="entrada"
            style={{ width: 'auto', padding: '0.2rem 0.4rem', fontSize: 'var(--t-xs)' }}
            value={tema}
            onChange={(e) => setTema(e.target.value as Tema)}
          >
            <option value="sistema">Sistema</option>
            <option value="claro">Claro</option>
            <option value="oscuro">Oscuro</option>
          </select>
        </label>
      </footer>

      {flujo.tipo === 'buscarTicket' && (
        <DialogoTickets
          titulo={
            flujo.motivo === 'Interrupcion'
              ? 'Elegir ticket de la interrupción'
              : 'Elegir ticket de la tarea principal'
          }
          contexto={ETIQUETA_ACCION[
            flujo.motivo === 'Interrupcion'
              ? 'RegistrarInterrupcion'
              : (flujo.motivo as TipoAccion)
          ]}
          onElegir={alElegirTicket}
          onCancelar={() => setFlujo({ tipo: 'ninguno' })}
        />
      )}

      {flujo.tipo === 'interrupcionDatos' && jornada && (
        <DialogoInterrupcion
          jornada={jornada}
          ticket={flujo.ticket}
          ahora={ahora}
          onConfirmar={(inicio, minutos) =>
            ejecutar({
              tipo: 'RegistrarInterrupcion',
              ticket: flujo.ticket,
              inicio,
              duracionMinutos: minutos,
              ahora: Date.now(),
            })
          }
          onCancelar={() => setFlujo({ tipo: 'ninguno' })}
        />
      )}

      {flujo.tipo === 'reabrir' && jornada && (
        <DialogoReapertura
          jornada={jornada}
          ahora={ahora}
          exportaciones={exportaciones}
          enviando={enviando}
          onConfirmar={(motivo, imputarIntervalo) =>
            ejecutar({
              tipo: 'ReabrirJornada',
              ahora: Date.now(),
              motivo,
              imputarIntervalo,
            })
          }
          onCancelar={() => setFlujo({ tipo: 'ninguno' })}
        />
      )}

      {flujo.tipo === 'confirmarFinEnDescanso' && (
        <Modal
          titulo="Cerrar la jornada durante un descanso"
          angosto
          onCerrar={() => setFlujo({ tipo: 'ninguno' })}
          pie={
            <>
              <button className="btn" onClick={() => setFlujo({ tipo: 'ninguno' })}>
                Volver
              </button>
              <button
                className="btn btn--peligro"
                disabled={enviando}
                onClick={() =>
                  ejecutar({
                    tipo: 'FinDia',
                    ahora: Date.now(),
                    confirmadoEnDescanso: true,
                  })
                }
              >
                Cerrar la jornada
              </button>
            </>
          }
        >
          <p style={{ margin: 0 }}>
            Estás en descanso. Si cerrás ahora, la jornada termina en el momento en que
            saliste al descanso y no se crea ninguna reanudación artificial.
          </p>
        </Modal>
      )}

      {flujo.tipo === 'esquema' && (
        <Modal
          titulo="Esquema simulado"
          contexto="Tablas y columnas que este prototipo asume. Ninguna se consulta realmente."
          onCerrar={() => setFlujo({ tipo: 'ninguno' })}
          pie={
            <button className="btn" onClick={() => setFlujo({ tipo: 'ninguno' })}>
              Cerrar
            </button>
          }
        >
          {ESQUEMA_SIMULADO.map((t) => (
            <div key={t.nombre} className="apilar" style={{ gap: 'var(--e-2)' }}>
              <div>
                <strong className="mono">{t.nombre}</strong>
                <div className="etiqueta" style={{ marginTop: 2 }}>
                  {t.origen}
                </div>
              </div>
              <p className="campo__ayuda" style={{ margin: 0 }}>
                {t.descripcion}
              </p>
              <div className="tabla-envoltorio">
                <table className="tabla">
                  <thead>
                    <tr>
                      <th>Columna</th>
                      <th>Tipo</th>
                      <th>Nota</th>
                    </tr>
                  </thead>
                  <tbody>
                    {t.columnas.map((c) => (
                      <tr key={c.nombre}>
                        <td className="num">{c.nombre}</td>
                        <td>{c.tipo}</td>
                        <td className="titulo-celda">{c.nota ?? ''}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ))}
        </Modal>
      )}
    </div>
  );
}
