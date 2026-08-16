/**
 * Persistencia del prototipo: localStorage del navegador.
 *
 * NO es una base de datos ni sustituye a una. Existe únicamente para demostrar
 * que recargar la página conserva el estado confirmado (AC-12). En el sistema real
 * cada transición se persiste en SQL Server antes de confirmar éxito al navegador.
 */

import type { Jornada, Usuario } from '../domain/tipos';

const CLAVE_JORNADA = 'asistente.prototipo.jornada';
const CLAVE_USUARIO = 'asistente.prototipo.usuario';
const CLAVE_TEMA = 'asistente.prototipo.tema';

function leer<T>(clave: string): T | null {
  try {
    const crudo = localStorage.getItem(clave);
    return crudo ? (JSON.parse(crudo) as T) : null;
  } catch {
    return null;
  }
}

function escribir(clave: string, valor: unknown): void {
  try {
    if (valor === null) localStorage.removeItem(clave);
    else localStorage.setItem(clave, JSON.stringify(valor));
  } catch {
    // Modo privado o cuota agotada: el prototipo sigue funcionando en memoria.
  }
}

/**
 * Las jornadas guardadas antes de existir la auditoría no traen el campo.
 * Se completa al leer para que el resto del código pueda asumirlo siempre presente.
 */
function normalizar(j: Jornada | null): Jornada | null {
  if (!j) return null;
  return { ...j, auditoria: j.auditoria ?? [] };
}

export const almacen = {
  leerJornada: () => normalizar(leer<Jornada>(CLAVE_JORNADA)),
  guardarJornada: (j: Jornada | null) => escribir(CLAVE_JORNADA, j),
  leerUsuario: () => leer<Usuario>(CLAVE_USUARIO),
  guardarUsuario: (u: Usuario | null) => escribir(CLAVE_USUARIO, u),
  // El oscuro es el tema elegido para esta aplicación; el claro queda disponible.
  leerTema: () => leer<'claro' | 'oscuro' | 'sistema'>(CLAVE_TEMA) ?? 'oscuro',
  guardarTema: (t: 'claro' | 'oscuro' | 'sistema') => escribir(CLAVE_TEMA, t),
};
