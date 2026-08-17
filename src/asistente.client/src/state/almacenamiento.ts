/**
 * Preferencias locales del navegador.
 *
 * La jornada YA NO se guarda acá: vive en el backend, que es su dueño. Recargar la página
 * no la conserva "de memoria", la vuelve a pedir (AC-12). Lo único que queda en el
 * navegador es lo que no tiene sentido guardar en el servidor: la sesión de trabajo
 * mientras no exista autenticación real, y el tema elegido.
 */

import type { Usuario } from '../domain/tipos';

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
    // Modo privado o cuota agotada: la aplicación sigue funcionando en memoria.
  }
}

export const almacen = {
  leerUsuario: () => leer<Usuario>(CLAVE_USUARIO),
  guardarUsuario: (u: Usuario | null) => escribir(CLAVE_USUARIO, u),
  // El oscuro es el tema elegido para esta aplicación; el claro queda disponible.
  leerTema: () => leer<'claro' | 'oscuro' | 'sistema'>(CLAVE_TEMA) ?? 'oscuro',
  guardarTema: (t: 'claro' | 'oscuro' | 'sistema') => escribir(CLAVE_TEMA, t),
};
