import { useMemo, useState } from 'react';

/**
 * Reglas que la interfaz verifica mientras se escribe.
 *
 * Es un espejo de la política del backend, para dar retroalimentación inmediata en vez de
 * hacer que la persona descubra los requisitos rebotando contra el servidor. El backend
 * la vuelve a aplicar entera: esto es ayuda, no control.
 */
export const REGLAS: { texto: string; cumple: (c: string) => boolean }[] = [
  { texto: 'Al menos 12 caracteres', cumple: (c) => c.length >= 12 },
  { texto: 'Una minúscula', cumple: (c) => /[a-zà-ÿ]/.test(c) },
  { texto: 'Una mayúscula', cumple: (c) => /[A-ZÀ-Ý]/.test(c) },
  { texto: 'Un número', cumple: (c) => /[0-9]/.test(c) },
  { texto: 'Un símbolo', cumple: (c) => /[^a-zA-Zà-ÿÀ-Ý0-9]/.test(c) },
];

export function claveCumpleReglas(clave: string): boolean {
  return REGLAS.every((r) => r.cumple(clave));
}

interface Props {
  id: string;
  etiqueta: string;
  valor: string;
  onCambiar: (valor: string) => void;
  /** Muestra la lista de requisitos debajo. Solo en registro y restablecimiento. */
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
