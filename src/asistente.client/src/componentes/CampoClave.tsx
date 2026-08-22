import { useMemo, useState } from 'react';

/**
 * Reglas que la interfaz verifica mientras se escribe.
 *
 * Es un espejo de la política del backend, para dar retroalimentación inmediata en vez de
 * hacer que la persona descubra los requisitos rebotando contra el servidor. El backend
 * la vuelve a aplicar entera: esto es ayuda, no control.
 *
 * Quedó en una sola regla al adoptarse una contraseña por defecto corta y numérica para
 * toda la nómina. Exigir doce caracteres con símbolo mientras la semilla usa cinco dígitos
 * habría sido una regla que el propio sistema incumple.
 */
export const LARGO_MINIMO = 4;

export const REGLAS: { texto: string; cumple: (c: string) => boolean }[] = [
  { texto: `Al menos ${LARGO_MINIMO} caracteres`, cumple: (c) => c.length >= LARGO_MINIMO },
];

export function claveCumpleReglas(clave: string): boolean {
  return REGLAS.every((r) => r.cumple(clave));
}

interface Props {
  id: string;
  etiqueta: string;
  valor: string;
  onCambiar: (valor: string) => void;
  /** Muestra la lista de requisitos debajo. Solo al elegir una contraseña nueva. */
  mostrarReglas?: boolean;
  autoComplete?: string;
}

export function CampoClave({
  id,
  etiqueta,
  valor,
  onCambiar,
  mostrarReglas = false,
  autoComplete = 'current-password',
}: Props) {
  const [visible, setVisible] = useState(false);
  const cumplidas = useMemo(() => REGLAS.map((r) => r.cumple(valor)), [valor]);

  return (
    <div className="campo">
      <label className="campo__etiqueta" htmlFor={id}>
        {etiqueta}
      </label>

      <div className="campo-clave">
        <input
          id={id}
          className="entrada"
          type={visible ? 'text' : 'password'}
          value={valor}
          onChange={(e) => onCambiar(e.target.value)}
          autoComplete={autoComplete}
        />
        <button
          type="button"
          className="btn btn--sutil campo-clave__ver"
          onClick={() => setVisible((v) => !v)}
          // Poder ver lo que se escribe reduce errores y no debilita nada: la contraseña
          // ya está en la pantalla de quien la escribe.
          aria-label={visible ? 'Ocultar contraseña' : 'Mostrar contraseña'}
        >
          {visible ? 'Ocultar' : 'Mostrar'}
        </button>
      </div>

      {mostrarReglas && (
        <ul className="reglas" aria-live="polite">
          {REGLAS.map((r, i) => (
            <li key={r.texto} className={cumplidas[i] ? 'regla regla--ok' : 'regla'}>
              <span aria-hidden="true">{cumplidas[i] ? '✓' : '○'}</span> {r.texto}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
