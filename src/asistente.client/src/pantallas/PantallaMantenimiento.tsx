import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { CampoClave, claveCumpleReglas } from '../componentes/CampoClave';
import { Modal } from '../componentes/Modal';
import {
  ErrorApi,
  mantenimientoApi,
  PERMISOS,
  type SesionApi,
  type UsuarioMantenimientoApi,
} from '../services/api';

interface Props {
  sesion: SesionApi;
  onVolver: () => void;
}

function fecha(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('es-AR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Mantenimiento de Usuarios.
 *
 * Las acciones NO viven dentro de la grilla. Como última columna de una tabla que scrollea
 * en horizontal quedaban fuera de alcance en pantallas angostas: había que arrastrar la
 * barra para poder pulsarlas. Ahora se elige una fila y se opera desde una barra fija
 * arriba, que no se mueve por más que la tabla se desplace.
 *
 * Las dos acciones son distintas a propósito. Desbloquear no toca la contraseña: si
 * alguien se equivocó cinco veces al tipear, obligarlo a estrenar contraseña sería
 * castigarlo por un error de dedos. Asignar una contraseña nueva, en cambio, además
 * destraba la cuenta, porque quien acaba de recibirla necesita poder usarla.
 *
 * Lo que NO se puede hacer desde acá es ver la contraseña de nadie: lo guardado es un
 * hash y la operación no tiene vuelta atrás.
 */
export function PantallaMantenimiento({ sesion, onVolver }: Props) {
  const [usuarios, setUsuarios] = useState<UsuarioMantenimientoApi[]>([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [nota, setNota] = useState<string | null>(null);
  const [reseteando, setReseteando] = useState<UsuarioMantenimientoApi | null>(null);

  // Se guarda el id y no la fila entera: al recargar la nómina los objetos son nuevos, y
  // una referencia vieja dejaría la barra operando sobre un estado que ya cambió.
  const [elegidoId, setElegidoId] = useState<number | null>(null);

  const puedeResetear = sesion.permisos.includes(PERMISOS.resetClave);
  const puedeDesbloquear = sesion.permisos.includes(PERMISOS.desbloquear);

  const elegido = usuarios.find((u) => u.id === elegidoId) ?? null;

  const cargar = useCallback(async () => {
    setCargando(true);
    try {
      setUsuarios(await mantenimientoApi.usuarios());
      setError(null);
    } catch (e) {
      setError(e instanceof ErrorApi ? e.message : 'No se pudo contactar el servidor.');
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => {
    void cargar();
  }, [cargar]);

  async function desbloquear() {
    if (!elegido) return;
    try {
      await mantenimientoApi.desbloquear(elegido.id);
      setNota('Cuenta de ' + elegido.usuario + ' desbloqueada.');
      await cargar();
    } catch (e) {
      setError(e instanceof ErrorApi ? e.message : 'No se pudo contactar el servidor.');
    }
  }

  // Desbloquear solo tiene sentido sobre una cuenta trabada o con intentos acumulados.
  const puedeDestrabar = elegido !== null && (elegido.bloqueado || elegido.intentosFallidos > 0);

  return (
    <div className="contenido contenido--unica">
      <section className="tarjeta">
        <header className="tarjeta__cabecera">
          <div>
            <span className="etiqueta">Administración</span>
            <h2 className="tarjeta__titulo">Mantenimiento de Usuarios</h2>
          </div>
          <button className="btn btn--sutil" onClick={onVolver}>
            ← Volver
          </button>
        </header>

        <div className="tarjeta__cuerpo">
          {nota && (
            <div className="aviso aviso--info" role="status">
              {nota}
            </div>
          )}

          {error && (
            <div className="aviso aviso--error" role="alert">
              {error}
            </div>
          )}

          {cargando ? (
            <p className="estado__vacio">Cargando la nómina…</p>
          ) : (
            <>
              <div className="barra-acciones">
                <span className="barra-acciones__objetivo" aria-live="polite">
                  {elegido ? (
                    <>
                      <strong className="mono">{elegido.usuario}</strong>
                      <span className="barra-acciones__nombre"> · {elegido.nombreCompleto}</span>
                    </>
                  ) : (
                    'Elegí un usuario de la lista'
                  )}
                </span>

                <div className="barra-acciones__botones">
                  {puedeDesbloquear && (
                    <button
                      className="btn"
                      onClick={() => void desbloquear()}
                      disabled={!puedeDestrabar}
                      title={
                        !elegido
                          ? 'Elegí un usuario'
                          : puedeDestrabar
                            ? 'Levantar el bloqueo'
                            : 'La cuenta no está bloqueada'
                      }
                    >
                      Desbloquear
                    </button>
                  )}

                  {puedeResetear && (
                    <button
                      className="btn"
                      onClick={() => elegido && setReseteando(elegido)}
                      disabled={!elegido}
                    >
                      Asignar contraseña
                    </button>
                  )}
                </div>
              </div>

              <div className="tabla-envoltorio">
                <table className="tabla tabla--compacta">
                  <thead>
                    <tr>
                      <th>
                        <span className="oculto-visual">Elegir</span>
                      </th>
                      <th>Usuario</th>
                      <th>Nombre</th>
                      <th>Estado</th>
                      <th>Último ingreso</th>
                      <th>Último cambio</th>
                    </tr>
                  </thead>
                  <tbody>
                    {usuarios.map((u) => (
                      <tr
                        key={u.id}
                        className={u.id === elegidoId ? 'tabla__fila--elegida' : undefined}
                        onClick={() => setElegidoId(u.id)}
                      >
                        <td>
                          {/*
                            El radio es el control accesible de verdad: se llega con Tab,
                            se recorre con las flechas y el lector de pantalla anuncia cuál
                            está elegido. El clic en cualquier parte de la fila es solo un
                            atajo cómodo por encima de eso.
                          */}
                          <input
                            type="radio"
                            name="usuario-elegido"
                            checked={u.id === elegidoId}
                            onChange={() => setElegidoId(u.id)}
                            aria-label={'Elegir ' + u.usuario}
                          />
                        </td>
                        <td className="mono">{u.usuario}</td>
                        <td>{u.nombreCompleto}</td>
                        <td>
                          {!u.activo ? (
                            <span className="tabla__estado tabla__estado--alerta">Inactivo</span>
                          ) : u.bloqueado ? (
                            <span
                              className="tabla__estado tabla__estado--alerta"
                              title={'Hasta ' + fecha(u.bloqueadoHastaUtc)}
                            >
                              Bloqueado
                            </span>
                          ) : (
                            <span className="tabla__estado">
                              Activo
                              {u.intentosFallidos > 0 && ' · ' + u.intentosFallidos + ' fallido(s)'}
                            </span>
                          )}
                        </td>
                        <td>{fecha(u.ultimoIngresoUtc)}</td>
                        <td>{fecha(u.ultimoCambioClaveUtc)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </div>
      </section>

      {reseteando && (
        <DialogoResetClave
          usuario={reseteando}
          onCerrar={() => setReseteando(null)}
          onListo={async (mensaje) => {
            setReseteando(null);
            setNota(mensaje);
            await cargar();
          }}
        />
      )}
    </div>
  );
}

function DialogoResetClave({
  usuario,
  onCerrar,
  onListo,
}: {
  usuario: UsuarioMantenimientoApi;
  onCerrar: () => void;
  onListo: (mensaje: string) => void;
}) {
  const [nueva, setNueva] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const coinciden = nueva === confirmacion;
  const puedeEnviar = claveCumpleReglas(nueva) && coinciden && !enviando;

  async function enviar(e: FormEvent) {
    e.preventDefault();
    if (!puedeEnviar) return;

    setEnviando(true);
    setError(null);

    try {
      await mantenimientoApi.resetClave(usuario.id, nueva, confirmacion);
      onListo(
        'Contraseña de ' + usuario.usuario + ' asignada. Avisale cuál es: nadie puede leerla después.',
      );
    } catch (e) {
      setError(e instanceof ErrorApi ? e.message : 'No se pudo contactar el servidor.');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Modal
      titulo={'Asignar contraseña a ' + usuario.usuario}
      contexto={usuario.nombreCompleto + ' · la cuenta queda destrabada al guardar.'}
      angosto
      onCerrar={onCerrar}
      notaPie="Anotala antes de guardar: no vas a poder consultarla después."
      pie={
        <>
          <button className="btn" type="button" onClick={onCerrar}>
            Cancelar
          </button>
          <button
            className="btn btn--principal"
            type="submit"
            form="form-reset-clave"
            disabled={!puedeEnviar}
          >
            {enviando ? 'Guardando…' : 'Asignar'}
          </button>
        </>
      }
    >
      <form id="form-reset-clave" onSubmit={enviar}>
        <CampoClave
          id="rc-nueva"
          etiqueta="Contraseña nueva"
          valor={nueva}
          onCambiar={setNueva}
          mostrarReglas
          autoComplete="new-password"
        />

        <CampoClave
          id="rc-confirmacion"
          etiqueta="Repetir"
          valor={confirmacion}
          onCambiar={setConfirmacion}
          autoComplete="new-password"
        />

        {confirmacion !== '' && !coinciden && (
          <div className="aviso" role="status">
            Las contraseñas no coinciden.
          </div>
        )}

        {error && (
          <div className="aviso aviso--error" role="alert">
            {error}
          </div>
        )}
      </form>
    </Modal>
  );
}
