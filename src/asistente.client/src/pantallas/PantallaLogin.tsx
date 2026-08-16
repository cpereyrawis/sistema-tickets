import { useState, type FormEvent } from 'react';
import { CLAVE_DEMO, USUARIOS } from '../mock/datos';
import type { Usuario } from '../domain/tipos';

/**
 * Inicio de sesión propio, sin SSO (decisión explícita para el prototipo).
 *
 * Valida contra la lista simulada de `mock/datos.ts`. En el sistema real esta pantalla
 * envía las credenciales al backend, que emite una cookie cifrada y descarta la
 * contraseña de inmediato: el navegador nunca ve credenciales de base de datos (FR-003).
 */
export function PantallaLogin({ onEntrar }: { onEntrar: (u: Usuario) => void }) {
  const [usuario, setUsuario] = useState('cpereyra');
  const [clave, setClave] = useState('');
  const [error, setError] = useState<string | null>(null);

  function enviar(e: FormEvent) {
    e.preventDefault();
    const fila = USUARIOS.find(
      (u) => u.USUARIO.toLowerCase() === usuario.trim().toLowerCase() && u.ACTIVO,
    );
    // Mensaje genérico: no revela si falló el usuario o la contraseña.
    if (!fila || clave !== CLAVE_DEMO) {
      setError('Usuario o contraseña incorrectos.');
      return;
    }
    onEntrar({ id: fila.USUARIO_ID, usuario: fila.USUARIO, nombre: fila.NOMBRE_COMPLETO });
  }

  return (
    <div className="login">
      <div className="login__caja">
        <div className="login__marca">
          <span className="etiqueta">Prototipo visual</span>
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
            />
          </div>

          <div className="campo">
            <label className="campo__etiqueta" htmlFor="l-clave">
              Contraseña
            </label>
            <input
              id="l-clave"
              className="entrada"
              type="password"
              value={clave}
              onChange={(e) => {
                setClave(e.target.value);
                setError(null);
              }}
              autoComplete="current-password"
            />
          </div>

          {error && <div className="aviso aviso--error">{error}</div>}

          <button className="btn btn--principal btn--grande" type="submit">
            Iniciar sesión
          </button>

          <div className="login__demo">
            <span>Datos simulados para probar el prototipo:</span>
            <span>
              usuario <code>cpereyra</code>, <code>mlopez</code> o <code>jdominguez</code> ·
              contraseña <code>{CLAVE_DEMO}</code>
            </span>
          </div>
        </form>
      </div>
    </div>
  );
}
