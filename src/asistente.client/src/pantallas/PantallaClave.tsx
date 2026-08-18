import { useEffect, useState, type FormEvent } from 'react';
import { CampoClave, claveCumpleReglas } from '../componentes/CampoClave';
import { authApi, ErrorApi } from '../services/api';

/**
 * Pedido de restablecimiento.
 *
 * Responde siempre lo mismo, exista o no la cuenta. Es deliberado: una respuesta distinta
 * convertiría esta pantalla en un verificador de qué correos están registrados.
 */
export function PantallaOlvidoClave({ onIrALogin }: { onIrALogin: () => void }) {
  const [emailLocal, setEmailLocal] = useState('');
  const [dominio, setDominio] = useState('@wis-software.com');
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  useEffect(() => {
    authApi.dominioCorreo().then((d) => setDominio(d.dominio)).catch(() => {});
  }, []);

  async function enviar(e: FormEvent) {
    e.preventDefault();
    if (enviando) return;

    setEnviando(true);
    setError(null);

    try {
      const r = await authApi.olvidoClave(emailLocal.trim());
      setMensaje(r.mensaje);
    } catch (e) {
      setError(e instanceof ErrorApi ? e.message : 'No se pudo contactar el servidor.');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="login">
      <div className="login__caja">
        <div className="login__marca">
          <span className="etiqueta">Recuperar acceso</span>
          <h1 className="login__titulo">Olvidé mi contraseña</h1>
          <p className="login__bajada">
            Te enviamos un enlace para elegir una nueva. Vence en una hora y sirve una sola vez.
          </p>
        </div>

        <form className="login__form" onSubmit={enviar}>
          {mensaje ? (
            <>
              <div className="aviso aviso--info">{mensaje}</div>
              <button className="btn btn--principal btn--grande" type="button" onClick={onIrALogin}>
                Volver al inicio de sesión
              </button>
            </>
          ) : (
            <>
              <div className="campo">
                <label className="campo__etiqueta" htmlFor="o-email">
                  Correo corporativo
                </label>
                <div className="campo-correo">
                  <input
                    id="o-email"
                    className="entrada"
                    value={emailLocal}
                    onChange={(e) => setEmailLocal(e.target.value)}
                    placeholder="tu.usuario"
                    autoComplete="username"
                    required
                  />
                  <span className="campo-correo__dominio">{dominio}</span>
                </div>
              </div>

              {error && <div className="aviso aviso--error">{error}</div>}

              <button
                className="btn btn--principal btn--grande"
                type="submit"
                disabled={enviando || emailLocal.trim() === ''}
              >
                {enviando ? 'Enviando…' : 'Enviar enlace'}
              </button>

              <div className="login__demo">
                <button type="button" className="enlace" onClick={onIrALogin}>
                  Volver al inicio de sesión
                </button>
              </div>
            </>
          )}
        </form>
      </div>
    </div>
  );
}

/** Elección de la nueva contraseña con el token que llegó por correo. */
export function PantallaRestablecerClave({
  token,
  onIrALogin,
}: {
  token: string;
  onIrALogin: () => void;
}) {
  const [clave, setClave] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [listo, setListo] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const coinciden = clave.length > 0 && clave === confirmacion;
  const puedeEnviar = claveCumpleReglas(clave) && coinciden && !enviando;

  async function enviar(e: FormEvent) {
    e.preventDefault();
    if (!puedeEnviar) return;

    setEnviando(true);
    setError(null);

    try {
      await authApi.restablecerClave(token, clave, confirmacion);
      setListo(true);
    } catch (e) {
      setError(e instanceof ErrorApi ? e.message : 'No se pudo contactar el servidor.');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="login">
      <div className="login__caja">
        <div className="login__marca">
          <span className="etiqueta">Recuperar acceso</span>
          <h1 className="login__titulo">Elegí una contraseña nueva</h1>
        </div>

        <form className="login__form" onSubmit={enviar}>
          {listo ? (
            <>
              <div className="aviso aviso--info">
                Tu contraseña quedó actualizada. Ya podés iniciar sesión.
              </div>
              <button className="btn btn--principal btn--grande" type="button" onClick={onIrALogin}>
                Iniciar sesión
              </button>
            </>
          ) : (
            <>
              <CampoClave
                id="n-clave"
                etiqueta="Nueva contraseña"
                valor={clave}
                onCambiar={setClave}
                mostrarReglas
                autoComplete="new-password"
              />

              <CampoClave
                id="n-confirmacion"
                etiqueta="Repetir contraseña"
                valor={confirmacion}
                onCambiar={setConfirmacion}
                autoComplete="new-password"
              />

              {confirmacion.length > 0 && !coinciden && (
                <div className="aviso aviso--error">Las contraseñas no coinciden.</div>
              )}

              {error && <div className="aviso aviso--error">{error}</div>}

              <button className="btn btn--principal btn--grande" type="submit" disabled={!puedeEnviar}>
                {enviando ? 'Guardando…' : 'Guardar contraseña'}
              </button>
            </>
          )}
        </form>
      </div>
    </div>
  );
}

/** Activación de la cuenta con el token del correo. Se dispara sola al abrir el enlace. */
export function PantallaVerificarEmail({
  token,
  onIrALogin,
}: {
  token: string;
  onIrALogin: () => void;
}) {
  const [estado, setEstado] = useState<'verificando' | 'ok' | 'error'>('verificando');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let vigente = true;

    authApi
      .verificarEmail(token)
      .then(() => vigente && setEstado('ok'))
      .catch((e) => {
        if (!vigente) return;
        setError(e instanceof ErrorApi ? e.message : 'No se pudo contactar el servidor.');
        setEstado('error');
      });

    return () => {
      vigente = false;
    };
  }, [token]);

  return (
    <div className="login">
      <div className="login__caja">
        <div className="login__marca">
          <span className="etiqueta">Activación</span>
          <h1 className="login__titulo">
            {estado === 'ok' ? 'Cuenta activada' : 'Verificando tu correo'}
          </h1>
        </div>

        <div className="login__form">
          {estado === 'verificando' && (
            <div className="aviso aviso--info">Estamos validando el enlace…</div>
          )}

          {estado === 'ok' && (
            <>
              <div className="aviso aviso--info">
                Tu correo quedó verificado. Ya podés iniciar sesión.
              </div>
              <button className="btn btn--principal btn--grande" onClick={onIrALogin}>
                Iniciar sesión
              </button>
            </>
          )}

          {estado === 'error' && (
            <>
              <div className="aviso aviso--error">{error}</div>
              <button className="btn btn--grande" onClick={onIrALogin}>
                Volver al inicio de sesión
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
