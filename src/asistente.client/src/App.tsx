import { useCallback, useEffect, useState } from 'react';
import { DialogoInterrupcion } from './componentes/DialogoInterrupcion';
import { DialogoReapertura } from './componentes/DialogoReapertura';
import { DialogoTickets } from './componentes/DialogoTickets';
import { Modal } from './componentes/Modal';
import { ETIQUETA_ACCION } from './domain/maquinaEstados';
import {
  filasExportacion,
  formatearFechaLarga,
  nombreArchivoExcel,
} from './domain/resumen';
import type { Jornada, TicketRef, TipoAccion } from './domain/tipos';
import { DialogoCambiarClave } from './componentes/DialogoCambiarClave';
import { PantallaLogin } from './pantallas/PantallaLogin';
import { PantallaMantenimiento } from './pantallas/PantallaMantenimiento';
import { PantallaPanel } from './pantallas/PantallaPanel';
import { PantallaRevision } from './pantallas/PantallaRevision';
import {
  authApi,
  devApi,
  ErrorApi,
  fallaActiva,
  jornadaApi,
  PERMISOS,
  simularFalla,
  type SesionApi,
} from './services/api';
import { descargar, generarXlsx } from './services/exportadorExcel';
import { almacen } from './state/almacenamiento';
import { ESQUEMA_SIMULADO } from './mock/esquema';

/** Diálogo abierto en este momento. */
type Flujo =
  | { tipo: 'ninguno' }
  | { tipo: 'buscarTicket'; motivo: 'ComenzarDia' | 'FinTarea' | 'Interrupcion' }
  | { tipo: 'interrupcionDatos'; ticket: TicketRef }
  | { tipo: 'confirmarFinEnDescanso' }
  | { tipo: 'reabrir' }
  | { tipo: 'esquema' };

type Tema = 'claro' | 'oscuro' | 'sistema';

/**
 * Qué se está mirando dentro de la aplicación ya autenticada.
 *
 * Alcanza con un estado y no hace falta enrutado por URL: al no existir registro ni
 * enlaces por correo, ninguna pantalla necesita abrirse desde una dirección directa.
 */
type Vista = 'panel' | 'revision' | 'mantenimiento';

export default function App() {
  const [usuario, setUsuario] = useState<SesionApi | null>(null);
  const [verificandoSesion, setVerificandoSesion] = useState(true);
  const [jornada, setJornada] = useState<Jornada | null>(null);
  const [flujo, setFlujo] = useState<Flujo>({ tipo: 'ninguno' });
  const [vista, setVista] = useState<Vista>('panel');
  const [menuAbierto, setMenuAbierto] = useState(false);
  const [cambiandoClave, setCambiandoClave] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [nota, setNota] = useState<string | null>(null);
  const [exportaciones, setExportaciones] = useState(0);
  const [enviando, setEnviando] = useState(false);
  const [cargando, setCargando] = useState(true);
  const [ahora, setAhora] = useState(() => Date.now());
  const [tema, setTema] = useState<Tema>(() => almacen.leerTema());
  const [falla, setFalla] = useState(() => fallaActiva());

  // Reloj del cronómetro. El transcurrido se calcula desde la hora de inicio que informa
  // el servidor, no acumulando en el cliente: así recargar no pierde precisión.
  useEffect(() => {
    const id = setInterval(() => setAhora(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    if (tema === 'sistema') document.documentElement.removeAttribute('data-theme');
    else document.documentElement.setAttribute('data-theme', tema === 'oscuro' ? 'dark' : 'light');
    almacen.guardarTema(tema);
  }, [tema]);

  // Al montar se pregunta al servidor si la cookie sigue siendo válida. Es lo que permite
  // recargar sin volver a escribir la contraseña, y también que la sesión caiga sola
  // cuando la cookie expira.
  useEffect(() => {
    let vigente = true;

    authApi
      .sesion()
      .then((s) => vigente && setUsuario(s))
      .catch(() => vigente && setUsuario(null))
      .finally(() => vigente && setVerificandoSesion(false));

    return () => {
      vigente = false;
    };
  }, []);

  /** Traduce un fallo de la API en el mensaje que ve el usuario. */
  const manejarError = useCallback((e: unknown) => {
    if (e instanceof ErrorApi) setError(e.message);
    else setError('No se pudo contactar el servidor. Verificá que el backend esté corriendo.');
  }, []);

  /** Recarga el estado vigente desde el servidor. */
  const recargar = useCallback(async () => {
    if (!usuario) return;
    setCargando(true);
    try {
      setJornada(await jornadaApi.actual());
      setError(null);
    } catch (e) {
      manejarError(e);
    } finally {
      setCargando(false);
    }
  }, [usuario, manejarError]);

  useEffect(() => {
    if (usuario) void recargar();
  }, [usuario, recargar]);

  // Al volver a la pestaña, el estado puede haber cambiado desde otro dispositivo.
  useEffect(() => {
    function alVolver() {
      if (document.visibilityState === 'visible') void recargar();
    }
    document.addEventListener('visibilitychange', alVolver);
    return () => document.removeEventListener('visibilitychange', alVolver);
  }, [recargar]);

  /**
   * Ejecuta una transición contra el backend.
   *
   * `enviando` bloquea el doble envío mientras la operación está en curso (§15.3). El
   * estado resultante NO se calcula acá: se toma tal cual lo devuelve el servidor.
   */
  const ejecutar = useCallback(
    async (operacion: () => Promise<Jornada>) => {
      if (enviando) return;
      setEnviando(true);
      setError(null);
      setNota(null);

      try {
        setJornada(await operacion());
        setFlujo({ tipo: 'ninguno' });
      } catch (e) {
        if (e instanceof ErrorApi && e.codigo === 'CONFIRMACION_REQUERIDA') {
          setFlujo({ tipo: 'confirmarFinEnDescanso' });
        } else {
          manejarError(e);
          setFlujo({ tipo: 'ninguno' });
          // Ante un conflicto de estado, lo que ve el usuario ya no es válido.
          if (e instanceof ErrorApi && e.status === 409) void recargar();
        }
      } finally {
        setEnviando(false);
      }
    },
    [enviando, manejarError, recargar],
  );

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

  /** Traduce el botón pulsado en un diálogo o en una llamada directa. */
  function alPulsarAccion(accion: TipoAccion) {
    setError(null);
    setNota(null);

    switch (accion) {
      case 'ComenzarDia':
      case 'FinTarea':
        setFlujo({ tipo: 'buscarTicket', motivo: accion });
        break;
      case 'RegistrarInterrupcion':
        setFlujo({ tipo: 'buscarTicket', motivo: 'Interrupcion' });
        break;
      case 'SalidaDescanso':
        void ejecutar(jornadaApi.salidaDescanso);
        break;
      case 'RegresoDescanso':
        void ejecutar(jornadaApi.regresoDescanso);
        break;
      case 'FinDia':
        void ejecutar(() => jornadaApi.finDia());
        break;
      case 'ReabrirJornada':
        setFlujo({ tipo: 'reabrir' });
        break;
    }
  }

  function alElegirTicket(ticket: TicketRef) {
    if (flujo.tipo !== 'buscarTicket') return;

    if (flujo.motivo === 'Interrupcion') {
      setFlujo({ tipo: 'interrupcionDatos', ticket });
      return;
    }

    // La marca temporal la pone el servidor: es la única hora confiable (§7.1).
    void ejecutar(() =>
      flujo.motivo === 'ComenzarDia'
        ? jornadaApi.comenzar(ticket.id)
        : jornadaApi.finTarea(ticket.id),
    );
  }

  function cerrarSesion() {
    // Se pide al servidor que elimine la cookie. Limpiarla solo del lado del cliente
    // dejaría una sesión válida circulando.
    void authApi.logout().catch(() => {});
    setUsuario(null);
    setJornada(null);
    setVista('panel');
    setFlujo({ tipo: 'ninguno' });
    setMenuAbierto(false);
  }

  if (verificandoSesion) {
    return (
      <div className="login">
        <div className="bloque">
          <span className="bloque__titulo">Verificando tu sesión…</span>
        </div>
      </div>
    );
  }

  if (!usuario) {
    return <PantallaLogin onEntrar={setUsuario} />;
  }

  const iniciales = usuario.nombreCompleto
    .split(' ')
    .slice(0, 2)
    .map((p) => p[0])
    .join('');

  return (
    <div className="app">
      <header className="barra">
        <div className="barra__fondo" aria-hidden="true" />

        <div className="barra__marca">
          <span className="barra__nombre">Asistente de Registro</span>
          <span className="barra__jornada">
            {jornada?.inicio ? formatearFechaLarga(jornada.inicio) : 'Sin jornada abierta'}
          </span>
        </div>

        <div className="menu">
          <button
            type="button"
            className="barra__usuario barra__usuario--boton"
            onClick={() => setMenuAbierto((v) => !v)}
            aria-haspopup="menu"
            aria-expanded={menuAbierto}
            // Sin esto el nombre accesible sería solo el nombre de la persona, que no
            // dice que el control abra nada.
            aria-label={'Opciones de ' + usuario.nombreCompleto}
          >
            <span className="avatar" aria-hidden="true">
              {iniciales}
            </span>
            <span>{usuario.nombreCompleto}</span>
            <span aria-hidden="true">▾</span>
          </button>

          {menuAbierto && (
            <>
              {/* Capa que cierra el menú al hacer clic afuera, que es lo que se espera
                  de un desplegable y evita dejarlo abierto tapando la pantalla. */}
              <div className="menu__velo" onClick={() => setMenuAbierto(false)} />

              <div className="menu__panel" role="menu">
                <button
                  className="menu__item"
                  role="menuitem"
                  onClick={() => {
                    setMenuAbierto(false);
                    setCambiandoClave(true);
                  }}
                >
                  Cambiar contraseña
                </button>

                {usuario.permisos.includes(PERMISOS.listar) && (
                  <button
                    className="menu__item"
                    role="menuitem"
                    onClick={() => {
                      setMenuAbierto(false);
                      setVista('mantenimiento');
                    }}
                  >
                    Mantenimiento de Usuarios
                  </button>
                )}

                <button className="menu__item menu__item--sep" role="menuitem" onClick={cerrarSesion}>
                  Cerrar sesión
                </button>
              </div>
            </>
          )}
        </div>
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

      {vista === 'mantenimiento' ? (
        <PantallaMantenimiento sesion={usuario} onVolver={() => setVista('panel')} />
      ) : cargando && !jornada ? (
        <main className="contenido contenido--unica">
          <div className="bloque">
            <span className="bloque__titulo">Cargando la jornada…</span>
          </div>
        </main>
      ) : vista === 'panel' || !jornada ? (
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
        <span>Conectado al backend · datos de tickets simulados en el servidor</span>
        <button className="btn btn--sutil" onClick={() => setFlujo({ tipo: 'esquema' })}>
          Ver esquema simulado
        </button>
        <button
          className="btn btn--sutil"
          disabled={enviando}
          onClick={() => {
            setNota(null);
            void ejecutar(devApi.jornadaEjemplo);
          }}
        >
          Cargar jornada de ejemplo
        </button>
        <button
          className="btn btn--sutil"
          disabled={enviando || !jornada?.id}
          onClick={() => {
            setNota(null);
            setExportaciones(0);
            void (async () => {
              try {
                await devApi.reiniciar();
                await recargar();
              } catch (e) {
                manejarError(e);
              }
            })();
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
          Simular caída de la conexión
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
            void ejecutar(() => jornadaApi.interrupcion(flujo.ticket.id, inicio, minutos))
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
                onClick={() => void ejecutar(() => jornadaApi.finDia(true))}
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

      {flujo.tipo === 'reabrir' && jornada && (
        <DialogoReapertura
          jornada={jornada}
          ahora={ahora}
          exportaciones={exportaciones}
          enviando={enviando}
          onConfirmar={(motivo, imputarIntervalo) =>
            void ejecutar(() => jornadaApi.reabrir(motivo, imputarIntervalo))
          }
          onCancelar={() => setFlujo({ tipo: 'ninguno' })}
        />
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

      {cambiandoClave && (
        <DialogoCambiarClave
          onCerrar={() => setCambiandoClave(false)}
          onListo={(mensaje) => {
            setCambiandoClave(false);
            setError(null);
            setNota(mensaje);
          }}
        />
      )}
    </div>
  );
}
