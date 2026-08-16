import { useEffect, useRef, type ReactNode } from 'react';

interface Props {
  titulo: string;
  contexto?: string;
  angosto?: boolean;
  onCerrar: () => void;
  pie: ReactNode;
  notaPie?: string;
  children: ReactNode;
}

/**
 * Contenedor de diálogo. Escape siempre cancela sin aplicar cambios (§15.2),
 * y el foco entra al diálogo al abrirse.
 */
export function Modal({ titulo, contexto, angosto, onCerrar, pie, notaPie, children }: Props) {
  const caja = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function alPresionar(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        e.stopPropagation();
        onCerrar();
      }
    }
    document.addEventListener('keydown', alPresionar);
    return () => document.removeEventListener('keydown', alPresionar);
  }, [onCerrar]);

  useEffect(() => {
    const primero = caja.current?.querySelector<HTMLElement>(
      'input, select, textarea, button',
    );
    primero?.focus();
  }, []);

  return (
    <div className="velo" onMouseDown={(e) => e.target === e.currentTarget && onCerrar()}>
      <div
        className={angosto ? 'modal modal--angosto' : 'modal'}
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-titulo"
        ref={caja}
      >
        <header className="modal__cabecera">
          <div>
            <h2 className="modal__titulo" id="modal-titulo">
              {titulo}
            </h2>
            {contexto && <p className="modal__contexto">{contexto}</p>}
          </div>
          <button className="btn btn--sutil" onClick={onCerrar} aria-label="Cerrar">
            ✕
          </button>
        </header>

        <div className="modal__cuerpo">{children}</div>

        <footer className="modal__pie">
          {notaPie && <span className="modal__pie-nota">{notaPie}</span>}
          <div className="modal__acciones">{pie}</div>
        </footer>
      </div>
    </div>
  );
}
