/**
 * Escritor de .xlsx — SOLO PARA EL PROTOTIPO.
 *
 * Produce un archivo Excel válido en el navegador, sin dependencias externas, usando el
 * perfil de columnas PROVISIONAL de §14.2. Un .xlsx es un ZIP de partes XML; acá se
 * arman esas partes y se empaquetan con método STORE (sin compresión), que Excel acepta.
 *
 * En el sistema final esto NO va en el navegador: la generación ocurre en el backend con
 * ClosedXML sobre la plantilla corporativa real, conservando sus fórmulas, formatos y
 * validaciones (FR-042, FR-043). Este módulo existe para poder demostrar el flujo de
 * descarga antes de tener esa plantilla, y debe eliminarse cuando exista el exportador
 * del servidor.
 */

import type { FilaExportacion } from '../domain/resumen';

/** Columnas del perfil provisional, en orden. */
const COLUMNAS: { encabezado: string; leer: (f: FilaExportacion) => string }[] = [
  { encabezado: 'Fecha', leer: (f) => f.fecha },
  { encabezado: 'Ticket', leer: (f) => f.ticket },
  { encabezado: 'Cliente', leer: (f) => f.cliente },
  { encabezado: 'Inicio', leer: (f) => f.inicio },
  { encabezado: 'Fin', leer: (f) => f.fin },
  { encabezado: 'Duración', leer: (f) => f.duracion },
  { encabezado: 'Tipo', leer: (f) => f.tipo },
  { encabezado: 'Motivo', leer: (f) => f.motivo },
];

const NOMBRE_HOJA = 'Registro';

// ---------- utilidades binarias ----------

function u16(n: number): Uint8Array {
  return new Uint8Array([n & 0xff, (n >>> 8) & 0xff]);
}

function u32(n: number): Uint8Array {
  return new Uint8Array([n & 0xff, (n >>> 8) & 0xff, (n >>> 16) & 0xff, (n >>> 24) & 0xff]);
}

function unir(partes: Uint8Array[]): Uint8Array {
  const largo = partes.reduce((a, p) => a + p.length, 0);
  const out = new Uint8Array(largo);
  let pos = 0;
  for (const p of partes) {
    out.set(p, pos);
    pos += p.length;
  }
  return out;
}

const TABLA_CRC = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n += 1) {
    let c = n;
    for (let k = 0; k < 8; k += 1) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();

function crc32(buf: Uint8Array): number {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i += 1) {
    c = TABLA_CRC[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  }
  return (c ^ 0xffffffff) >>> 0;
}

// ---------- ZIP (método STORE) ----------

interface Parte {
  nombre: string;
  datos: Uint8Array;
}

function armarZip(partes: Parte[]): Uint8Array {
  const ahora = new Date();
  const horaDos =
    ((ahora.getHours() << 11) |
      (ahora.getMinutes() << 5) |
      Math.floor(ahora.getSeconds() / 2)) &
    0xffff;
  const fechaDos =
    (((ahora.getFullYear() - 1980) << 9) | ((ahora.getMonth() + 1) << 5) | ahora.getDate()) &
    0xffff;

  const locales: Uint8Array[] = [];
  const centrales: Uint8Array[] = [];
  let desplazamiento = 0;

  for (const p of partes) {
    const nombre = new TextEncoder().encode(p.nombre);
    const crc = crc32(p.datos);
    const tamano = p.datos.length;

    const encabezadoLocal = unir([
      u32(0x04034b50),
      u16(20), // versión necesaria
      u16(0), // banderas
      u16(0), // método: STORE
      u16(horaDos),
      u16(fechaDos),
      u32(crc),
      u32(tamano), // comprimido
      u32(tamano), // sin comprimir
      u16(nombre.length),
      u16(0), // extra
      nombre,
    ]);

    locales.push(encabezadoLocal, p.datos);

    centrales.push(
      unir([
        u32(0x02014b50),
        u16(20), // versión de creación
        u16(20), // versión necesaria
        u16(0),
        u16(0),
        u16(horaDos),
        u16(fechaDos),
        u32(crc),
        u32(tamano),
        u32(tamano),
        u16(nombre.length),
        u16(0), // extra
        u16(0), // comentario
        u16(0), // disco
        u16(0), // atributos internos
        u32(0), // atributos externos
        u32(desplazamiento),
        nombre,
      ]),
    );

    desplazamiento += encabezadoLocal.length + tamano;
  }

  const directorio = unir(centrales);
  const fin = unir([
    u32(0x06054b50),
    u16(0),
    u16(0),
    u16(partes.length),
    u16(partes.length),
    u32(directorio.length),
    u32(desplazamiento),
    u16(0),
  ]);

  return unir([...locales, directorio, fin]);
}

// ---------- partes XML del libro ----------

function escapar(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/** Índice 0 → "A", 25 → "Z", 26 → "AA". */
function letraColumna(indice: number): string {
  let n = indice + 1;
  let out = '';
  while (n > 0) {
    const resto = (n - 1) % 26;
    out = String.fromCharCode(65 + resto) + out;
    n = Math.floor((n - resto) / 26);
  }
  return out;
}

function celda(fila: number, columna: number, texto: string, estilo: number): string {
  if (texto === '') return '';
  return (
    `<c r="${letraColumna(columna)}${fila}" t="inlineStr" s="${estilo}">` +
    `<is><t xml:space="preserve">${escapar(texto)}</t></is></c>`
  );
}

function hojaXml(filas: FilaExportacion[]): string {
  const encabezado = COLUMNAS.map((c, i) => celda(1, i, c.encabezado, 1)).join('');
  const cuerpo = filas
    .map((f, iFila) => {
      const celdas = COLUMNAS.map((c, iCol) => celda(iFila + 2, iCol, c.leer(f), 0)).join('');
      return `<row r="${iFila + 2}">${celdas}</row>`;
    })
    .join('');

  return (
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
    '<cols>' +
    '<col min="1" max="1" width="12" customWidth="1"/>' +
    '<col min="2" max="2" width="14" customWidth="1"/>' +
    '<col min="3" max="3" width="28" customWidth="1"/>' +
    '<col min="4" max="6" width="10" customWidth="1"/>' +
    '<col min="7" max="7" width="14" customWidth="1"/>' +
    '<col min="8" max="8" width="46" customWidth="1"/>' +
    '</cols>' +
    `<sheetData><row r="1">${encabezado}</row>${cuerpo}</sheetData>` +
    '</worksheet>'
  );
}

const CONTENT_TYPES =
  '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
  '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
  '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
  '<Default Extension="xml" ContentType="application/xml"/>' +
  '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>' +
  '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' +
  '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>' +
  '</Types>';

const RELS_RAIZ =
  '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
  '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
  '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>' +
  '</Relationships>';

const WORKBOOK =
  '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
  '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" ' +
  'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">' +
  `<sheets><sheet name="${NOMBRE_HOJA}" sheetId="1" r:id="rId1"/></sheets>` +
  '</workbook>';

const RELS_WORKBOOK =
  '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
  '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
  '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>' +
  '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>' +
  '</Relationships>';

const STYLES =
  '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
  '<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
  '<fonts count="2">' +
  '<font><sz val="11"/><name val="Calibri"/></font>' +
  '<font><b/><sz val="11"/><name val="Calibri"/></font>' +
  '</fonts>' +
  '<fills count="2"><fill><patternFill patternType="none"/></fill>' +
  '<fill><patternFill patternType="gray125"/></fill></fills>' +
  '<borders count="1"><border/></borders>' +
  '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>' +
  '<cellXfs count="2">' +
  '<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>' +
  '<xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>' +
  '</cellXfs>' +
  '</styleSheet>';

// ---------- API pública ----------

const TIPO_XLSX =
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

export function generarXlsx(filas: FilaExportacion[]): Blob {
  const texto = (s: string) => new TextEncoder().encode(s);

  const zip = armarZip([
    { nombre: '[Content_Types].xml', datos: texto(CONTENT_TYPES) },
    { nombre: '_rels/.rels', datos: texto(RELS_RAIZ) },
    { nombre: 'xl/workbook.xml', datos: texto(WORKBOOK) },
    { nombre: 'xl/_rels/workbook.xml.rels', datos: texto(RELS_WORKBOOK) },
    { nombre: 'xl/styles.xml', datos: texto(STYLES) },
    { nombre: 'xl/worksheets/sheet1.xml', datos: texto(hojaXml(filas)) },
  ]);

  return new Blob([zip as BlobPart], { type: TIPO_XLSX });
}

export function descargar(blob: Blob, nombreArchivo: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = nombreArchivo;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
