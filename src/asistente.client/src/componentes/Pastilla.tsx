import { etiquetaEstado } from '../domain/maquinaEstados';
import type { EstadoJornada } from '../domain/tipos';

export function Pastilla({ estado }: { estado: EstadoJornada }) {
  return (
    <span className={`pastilla pastilla--${estado}`}>
      <span className="pastilla__punto" aria-hidden="true" />
      {etiquetaEstado(estado)}
    </span>
  );
}
