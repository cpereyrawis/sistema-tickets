# Prototipo visual — Asistente de Registro de Tareas

Prototipo **estrictamente visual** de la especificación v2.0. Sirve para validar los flujos
de jornada y la interfaz antes de construir el backend.

```bash
npm install --prefix src/asistente.client
```

```bash
npm run dev --prefix src/asistente.client
```

Abre en `http://localhost:5173`. Usuarios: `cpereyra`, `mlopez` o `jdominguez`; contraseña `demo`.

## Qué es real y qué está simulado

| Área | En el prototipo | En el sistema final |
|---|---|---|
| Máquina de estados | Implementada completa, como función pura en el cliente | Misma lógica en `Asistente.Domain`, ejecutada en el backend |
| Datos de tickets | Arreglo en memoria (`src/mock/datos.ts`) con latencia artificial | Vista corporativa de solo lectura vía `ITicketQueryService` |
| Autenticación | Lista simulada, sin SSO | Cookie ASP.NET Core + `IUserAuthenticator` |
| Persistencia | `localStorage`, solo para demostrar que recargar conserva el estado | SQL Server + EF Core, una transacción por transición |
| Excel | Genera un `.xlsx` real en el navegador con un perfil de columnas provisional | ClosedXML en el backend, sobre la plantilla corporativa real |

**No hay ninguna consulta a base de datos.** El esquema que el prototipo asume está
documentado en `src/mock/esquema.ts` y se puede ver desde el pie de la aplicación.

## Comportamientos de la especificación que el prototipo demuestra

- **AC-01** Sin jornada abierta, "Comenzar el día" es la única acción operativa.
- **AC-03** El cambio de tarea cierra la anterior e inicia la siguiente en la misma marca temporal.
- **AC-04** Cancelar la selección de ticket (Escape o "Cancelar") no cierra la tarea vigente.
- **AC-05 / AC-06** La interrupción genera exactamente cuatro eventos con `CorrelationId` común, y su fin es inicio + duración.
- **AC-07 / AC-08** El descanso cierra la sesión sin imputar tiempo; el regreso reanuda el mismo ticket.
- **AC-09** La jornada finalizada no admite nuevas acciones.
- **AC-10** Los tickets se listan por fecha de creación descendente, con paginación.
- **AC-12** Recargar el navegador conserva el estado confirmado.
- **§8.1** Estados de carga, sin resultados y error de conexión claramente diferenciados
  (el interruptor del pie simula la caída de la fuente).
- **§15.3** Doble envío bloqueado, hora de fin visible antes de confirmar, botones
  incompatibles ocultos.
- **FR-035 / NFR-007** Una jornada cerrada por error se puede reabrir como corrección
  auditada. El usuario elige si el intervalo transcurrido se imputa a la tarea principal
  o queda como hueco; la elección, el motivo, el actor y la hora quedan registrados y se
  muestran en la revisión.
- **FR-041** Se detectan huecos, solapamientos y tramos con fin anterior al inicio. Los
  descansos y las reaperturas no se cuentan como anomalía.

## Decisiones aplicadas del plan de implementación

- **D-5** El botón de regreso dice "Registrar regreso del descanso", la alternativa que la
  propia especificación sugiere por ser menos ambigua.
- **D-6** Se permite cerrar el día durante un descanso, con confirmación explícita y sin
  crear una reanudación artificial. La jornada queda datada al fin del último tramo real.
- **D-7** El perfil de columnas del Excel es provisional y está marcado como tal en la
  pantalla de revisión.
- **D-8** Se permite corregir después de exportar; el diálogo de reapertura avisa que hay
  que regenerar el archivo y la nueva copia queda identificada como regeneración.

## Estética

El sistema visual deriva del panel corporativo de Recibos Digitales: naranja de marca
`#ff7f04`, radio de 1rem, la sombra de tinte violáceo y el hover `scale(1.03)`. La
traducción a modo oscuro conserva el tono de marca bajando la luminosidad, en vez de
invertir los colores. El degradado de la banda superior va más oscuro que el original
para que el texto blanco supere 4.5:1 de contraste.

## Estructura

```
src/
├─ domain/       Máquina de estados, tipos y cálculos derivados (puro, sin React)
├─ services/     Adaptador de tickets — única pieza a reemplazar por HTTP real
├─ mock/         Esquema y datos de ejemplo
├─ componentes/  Diálogos y piezas reutilizables
├─ pantallas/    Login, panel, revisión
└─ styles/       Tokens de diseño y hoja de estilos
```

`domain/` no importa nada de React ni de `services/`: es la capa que migra tal cual a C#.
