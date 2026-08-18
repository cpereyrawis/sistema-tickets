import { useState, type FormEvent } from 'react';
import { CampoClave } from '../componentes/CampoClave';
import { authApi, ErrorApi, type SesionApi } from '../services/api';

interface Props {
  onEntrar: (sesion: SesionApi) => void;
  onIrARegistro: () => void;
  onIrAOlvido: () => void;
}

/**
 * Inicio de sesión.
 *
 * Las credenciales viajan al backend, que valida y emite una cookie cifrada. La contraseña
 * no se guarda en ningún estado persistente del cliente ni vuelve en la respuesta
 * (FR-003, AC-16).
 */
export function PantallaLogin({ onEntrar, onIrARegistro, onIrAOlvido }: Props) {
  const [usuario, setUsuario] = useState('');
  const [clave, setClave] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function enviar(e: FormEvent) {
    e.preventDefault();
    if (enviando) return;

    setEnviando(true);
    setError(null);

    try {
      onEntrar(await authApi.login(usuario.trim(), clave));
    } catch (e) {
      // El backend responde igual ante usuario inexistente y contraseña equivocada; acá
      // solo se muestra lo que dijo, sin agregar pistas.
      setError(e instanceof ErrorApi ? e.message : 'No se pudo contactar el servidor.');
      setClave('');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="login">
      <div className="login__caja">
        <div className="login__marca">
          <span className="etiqueta">Iniciar sesión</span>
          <h1 className="login__titulo">Asistente de Registro de Tareas</h1>
          <p className="login__bajada">
            Registrar el trabajo debe requerir menos atención que realizarlo.
          </p>
        </div>

        <form className="login__form" onSubmit={enviar}>
          <div className="campo">
            <label className="campo__etiqueta" htmlFor="l-usuario">
              Usuario
            </label>
            <input
              id="l-usuario"
              className="entrada"
              value={usuario}
              onChange={(e) => {
                setUsuario(e.target.value);
                setError(null);
              }}
              autoComplete="username"
              required
            />
          </div>

          <CampoClave id="l-clave" etiqueta="Contraseña" valor={clave} onCambiar={setClave} />

          {error && (
            <div className="aviso aviso--error" role="alert">
              {error}
            </div>
          )}

          <button
            className="btn btn--principal btn--grande"
            type="submit"
            disabled={enviando || usuario.trim() === '' || clave === ''}
          >
            {enviando ? 'Ingresando…' : 'Iniciar sesión'}
          </button>

          <div className="login__demo">
            <span>
              <button type="button" className="enlace" onClick={onIrAOlvido}>
                Olvidé mi contraseña
              </button>
            </span>
            <span>
              ¿No tenés cuenta?{' '}
              <button type="button" className="enlace" onClick={onIrARegistro}>
                Registrate
              </button>
            </span>
          </div>
        </form>
      </div>
    </div>
  );
}
