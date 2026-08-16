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

export const almacen = {
  leerJornada: () => leer<Jornada>(CLAVE_JORNADA),
  guardarJornada: (j: Jornada | null) => escribir(CLAVE_JORNADA, j),
  leerUsuario: () => leer<Usuario>(CLAVE_USUARIO),
  guardarUsuario: (u: Usuario | null) => escribir(CLAVE_USUARIO, u),
  leerTema: () => leer<'claro' | 'oscuro' | 'sistema'>(CLAVE_TEMA) ?? 'sistema',
  guardarTema: (t: 'claro' | 'oscuro' | 'sistema') => escribir(CLAVE_TEMA, t),
};
