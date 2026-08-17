import { useEffect, useMemo, useState } from 'react';
import { Modal } from './Modal';
import { ticketsApi, type ClienteApi, type TicketApi } from '../services/api';
import { formatearFecha, formatearHora } from '../domain/resumen';
import type { TicketRef } from '../domain/tipos';

interface Props {
  titulo: string;
  contexto: string;
  onElegir: (ticket: TicketRef) => void;
  onCancelar: () => void;
}

const TAMANO = 6;

/**
 * Pantalla de consulta de tickets (§8).
 * La acción que originó la consulta permanece visible en el encabezado (§15.2).
 * Enter confirma la selección y Escape cancela sin cambios.
 */
export function DialogoTickets({ titulo, contexto, onElegir, onCancelar }: Props) {
  const [clientes, setClientes] = useState<ClienteApi[]>([]);
  const [clienteId, setClienteId] = useState('');
  const [texto, setTexto] = useState('');
  const [tickets, setTickets] = useState<TicketApi[]>([]);
  const [total, setTotal] = useState(0);
  const [pagina, setPagina] = useState(1);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [seleccionado, setSeleccionado] = useState<string | null>(null);

  useEffect(() => {
    let vigente = true;
    ticketsApi
      .clientes()
      .then((c) => vigente && setClientes(c))
      .catch(() => vigente && setClientes([]));
    return () => {
      vigente = false;
    };
  }, []);

  useEffect(() => {
    let vigente = true;
    setCargando(true);
    setError(null);

    // Búsqueda incremental con una pausa corta, para no consultar en cada tecla.
    const id = setTimeout(() => {
      ticketsApi
        .buscar({ clienteId: clienteId || undefined, texto, pagina, tamano: TAMANO })
        .then((r) => {
          if (!vigente) return;
          setTickets(r.items);
          setTotal(r.total);
          setCargando(false);
        })
        .catch((e: Error) => {
          if (!vigente) return;
          setError(e.message);
          setTickets([]);
          setTotal(0);
          setCargando(false);
        });
    }, 180);

    return () => {
      vigente = false;
      clearTimeout(id);
    };
  }, [clienteId, texto, pagina]);

  const elegido = useMemo(
    () => tickets.find((t) => t.ticketId === seleccionado) ?? null,
    [tickets, seleccionado],
  );

  function confirmar(t: TicketApi | null) {
    if (!t) return;
    onElegir({
      id: t.ticketId,
      clienteId: t.clienteId,
      clienteNombre: t.clienteNombre,
      titulo: t.titulo,
    });
  }

  const ultimaPagina = Math.max(1, Math.ceil(total / TAMANO));

  return (
    <Modal
      titulo={titulo}
      contexto={contexto}
      onCerrar={onCancelar}
      notaPie={
        total > 0
          ? `${total} ticket${total === 1 ? '' : 's'} · orden por fecha de creación descendente`
          : undefined
      }
      pie={
        <>
          <button className="btn" onClick={onCancelar}>
            Cancelar
          </button>
          <button
            className="btn btn--principal"
            disabled={!elegido}
            onClick={() => confirmar(elegido)}
          >
            Confirmar ticket
          </button>
        </>
      }
    >
      <div className="filtros">
        <div className="campo">
          <label className="campo__etiqueta" htmlFor="f-cliente">
            Cliente
          </label>
          <select
            id="f-cliente"
            className="entrada"
            value={clienteId}
            onChange={(e) => {
              setClienteId(e.target.value);
              setPagina(1);
              setSeleccionado(null);
            }}
          >
            <option value="">Todos los clientes</option>
            {clientes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="campo">
          <label className="campo__etiqueta" htmlFor="f-texto">
            Buscar por ticket o título
          </label>
          <input
            id="f-texto"
            className="entrada"
            type="search"
            placeholder="SUP-14892, remito, sincroniza…"
            value={texto}
            onChange={(e) => {
              setTexto(e.target.value);
              setPagina(1);
              setSeleccionado(null);
            }}
          />
        </div>
      </div>

      <div className="lista">
        {error && (
          <div className="bloque bloque--error">
            <span className="bloque__titulo">No se pudo consultar la fuente de tickets</span>
            <span>
              La jornada ya registrada no se ve afectada. Podés reintentar en unos segundos.
            </span>
          </div>
        )}

        {!error && cargando && (
          <div className="bloque">
            <span className="bloque__titulo">Consultando tickets…</span>
          </div>
        )}

        {!error && !cargando && tickets.length === 0 && (
          <div className="bloque">
            <span className="bloque__titulo">Sin resultados</span>
            <span>Probá con otro cliente o quitá parte del texto buscado.</span>
          </div>
        )}

        {!error &&
          !cargando &&
          tickets.map((t) => (
            <button
              key={t.ticketId}
              className="ticket"
              aria-selected={seleccionado === t.ticketId}
              onClick={() => setSeleccionado(t.ticketId)}
              onDoubleClick={() => confirmar(t)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  confirmar(t);
                }
              }}
            >
              <span className="ticket__id">{t.ticketId}</span>
              <span className="ticket__medio">
                <span className="ticket__titulo">{t.titulo}</span>
                <span className="ticket__cliente">{t.clienteNombre}</span>
              </span>
              <span className="ticket__derecha">
                <span className="marca-estado">{t.estado}</span>
                <span className="ticket__fecha">
                  {formatearFecha(t.creadoEn)} {formatearHora(t.creadoEn)}
                </span>
              </span>
            </button>
          ))}
      </div>

      {!error && ultimaPagina > 1 && (
        <div className="fila" style={{ gap: 'var(--e-2)', justifyContent: 'center' }}>
          <button
            className="btn btn--sutil"
            disabled={pagina <= 1}
            onClick={() => setPagina((p) => p - 1)}
          >
            ← Anterior
          </button>
          <span className="etiqueta">
            Página {pagina} de {ultimaPagina}
          </span>
          <button
            className="btn btn--sutil"
            disabled={pagina >= ultimaPagina}
            onClick={() => setPagina((p) => p + 1)}
          >
            Siguiente →
          </button>
        </div>
      )}
    </Modal>
  );
}
