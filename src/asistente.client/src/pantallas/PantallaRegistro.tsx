import { useEffect, useState, type FormEvent } from 'react';
import { CampoClave, claveCumpleReglas } from '../componentes/CampoClave';
import { authApi, ErrorApi, type UsuarioHabilitadoApi } from '../services/api';

interface Props {
  onIrALogin: () => void;
}

/**
 * Alta de cuenta.
 *
 * El nombre de usuario sale de una lista cerrada y el dominio del correo es fijo: la
 * aplicación es interna y solo debe poder registrarse quien ya existe en el sistema de
 * tickets. El backend vuelve a validar ambas cosas; que acá sean un desplegable y un
 * sufijo de solo lectura es comodidad, no seguridad.
 */
export function PantallaRegistro({ onIrALogin }: Props) {
  const [habilitados, setHabilitados] = useState<UsuarioHabilitadoApi[]>([]);
  const [dominio, setDominio] = useState('@wis-software.com');
  const [usuario, setUsuario] = useState('');
  const [emailLocal, setEmailLocal] = useState('');
  const [clave, setClave] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [listo, setListo] = useState<{ email: string; enlace: string | null } | null>(null);

  useEffect(() => {
    authApi.usuariosHabilitados().then(setHabilitados).catch(() => setHabilitados([]));
    authApi.dominioCorreo().then((d) => setDominio(d.dominio)).catch(() => {});
  }, []);

  // Al elegir el usuario se propone la misma cadena como parte local del correo, que es
  // lo habitual. Sigue siendo editable por si no coinciden.
  function elegirUsuario(valor: string) {
    setUsuario(valor);
    if (emailLocal === '' || habilitados.some((h) => h.usuario === emailLocal)) {
      setEmailLocal(valor);
    }
  }

  const coinciden = clave.length > 0 && clave === confirmacion;
  const puedeEnviar =
    usuario !== '' && emailLocal.trim() !== '' && claveCumpleReglas(clave) && coinciden && !enviando;

  async function enviar(e: FormEvent) {
    e.preventDefault();
    if (!puedeEnviar) return;

    setEnviando(true);
    setError(null);

    try {
      const r = await authApi.registro({
        usuario,
        emailLocal: emailLocal.trim(),
        clave,
        claveConfirmacion: confirmacion,
      });
      setListo({ email: r.email, enlace: r.enlaceVerificacion });
    } catch (e) {
      setError(e instanceof ErrorApi ? e.message : 'No se pudo completar el registro.');
    } finally {
      setEnviando(false);
    }
  }

  if (listo) {
    return (
      <div className="login">
        <div className="login__caja">
          <div className="login__marca">
            <span className="etiqueta">Cuenta creada</span>
            <h1 className="login__titulo">Revisá tu correo</h1>
          </div>

          <div className="login__form">
            <p style={{ margin: 0, color: 'var(--texto-2)' }}>
              Enviamos un enlace de activación a{' '}
              <strong style={{ color: 'var(--texto)' }}>{listo.email}</strong>. Hasta que lo
              uses, la cuenta no puede iniciar sesión.
            </p>

            {listo.enlace && (
              <div className="aviso aviso--alerta">
                <div>
                  <strong>Modo desarrollo</strong>
                  <div style={{ marginTop: 4 }}>
                    No hay servidor de correo configurado, así que el enlace se muestra acá.
                    En cualquier otro entorno esto no aparece.
                  </div>
                  <a href={listo.enlace} style={{ color: 'var(--acento)', wordBreak: 'break-all' }}>
                    {listo.enlace}
                  </a>
                </div>
              </div>
            )}

            <button className="btn btn--principal btn--grande" onClick={onIrALogin}>
              Ir al inicio de sesión
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="login">
      <div className="login__caja">
        <div className="login__marca">
          <span className="etiqueta">Crear cuenta</span>
          <h1 className="login__titulo">Asistente de Registro de Tareas</h1>
          <p className="login__bajada">
            Usá el mismo nombre de usuario que tenés en el sistema de tickets.
          </p>
        </div>

        <form className="login__form" onSubmit={enviar}>
          <div className="campo">
            <label className="campo__etiqueta" htmlFor="r-usuario">
              Nombre de usuario
            </label>
            <select
              id="r-usuario"
              className="entrada"
              value={usuario}
              onChange={(e) => elegirUsuario(e.target.value)}
              required
            >
              <option value="">Elegí tu usuario…</option>
              {habilitados.map((h) => (
                <option key={h.usuario} value={h.usuario}>
                  {h.usuario} — {h.nombreCompleto}
                </option>
              ))}
            </select>
            <span className="campo__ayuda">
              Es el que vincula tu cuenta con tus tickets. Si no está en la lista, pedí el alta.
            </span>
          </div>

          <div className="campo">
            <label className="campo__etiqueta" htmlFor="r-email">
              Correo corporativo
            </label>
            <div className="campo-correo">
              <input
                id="r-email"
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

          <CampoClave
            id="r-clave"
            etiqueta="Contraseña"
            valor={clave}
            onCambiar={setClave}
            mostrarReglas
            autoComplete="new-password"
          />

          <CampoClave
            id="r-confirmacion"
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
            {enviando ? 'Creando cuenta…' : 'Crear cuenta'}
          </button>

          <div className="login__demo">
            <span>
              ¿Ya tenés cuenta?{' '}
              <button type="button" className="enlace" onClick={onIrALogin}>
                Iniciar sesión
              </button>
            </span>
          </div>
        </form>
      </div>
    </div>
  );
}
