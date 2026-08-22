import { useState, type FormEvent } from 'react';
import { CampoClave, claveCumpleReglas } from './CampoClave';
import { Modal } from './Modal';
import { authApi, ErrorApi } from '../services/api';

interface Props {
  onCerrar: () => void;
  onListo: (mensaje: string) => void;
}

/**
 * Cambio de la propia contraseña.
 *
 * Pide la actual aunque la sesión ya esté abierta. No es burocracia: es lo que impide que
 * una sesión olvidada en una máquina ajena se convierta en una cuenta perdida.
 */
export function DialogoCambiarClave({ onCerrar, onListo }: Props) {
  const [actual, setActual] = useState('');
  const [nueva, setNueva] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const coinciden = nueva === confirmacion;
  const puedeEnviar =
    actual !== '' && claveCumpleReglas(nueva) && coinciden && !enviando;

  async function enviar(e: FormEvent) {
    e.preventDefault();
    if (!puedeEnviar) return;

    setEnviando(true);
    setError(null);

    try {
      await authApi.cambiarClave(actual, nueva, confirmacion);
      onListo('Contraseña actualizada.');
    } catch (e) {
      setError(e instanceof ErrorApi ? e.message : 'No se pudo contactar el servidor.');
      setActual('');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Modal
      titulo="Cambiar contraseña"
      contexto="La nueva empieza a regir de inmediato. La sesión actual sigue abierta."
      angosto
      onCerrar={onCerrar}
      pie={
        <>
          <button className="btn" type="button" onClick={onCerrar}>
            Cancelar
          </button>
          <button
            className="btn btn--principal"
            type="submit"
            form="form-cambiar-clave"
            disabled={!puedeEnviar}
          >
            {enviando ? 'Guardando…' : 'Guardar'}
          </button>
        </>
      }
    >
      <form id="form-cambiar-clave" onSubmit={enviar}>
        <CampoClave
          id="cc-actual"
          etiqueta="Contraseña actual"
          valor={actual}
          onCambiar={(v) => {
            setActual(v);
            setError(null);
          }}
          autoComplete="current-password"
        />

        <CampoClave
          id="cc-nueva"
          etiqueta="Contraseña nueva"
          valor={nueva}
          onCambiar={setNueva}
          mostrarReglas
          autoComplete="new-password"
        />

        <CampoClave
          id="cc-confirmacion"
          etiqueta="Repetir la nueva"
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
