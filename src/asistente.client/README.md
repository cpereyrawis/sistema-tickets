# Frontend — Asistente de Registro de Tareas

Interfaz React conectada al backend `Asistente.Api`. **Necesita el backend corriendo**:
el estado de la jornada lo decide y lo guarda el servidor.

Hay que levantar los dos procesos:

```bash
dotnet run --project src/Asistente.Api --no-launch-profile --urls http://localhost:5290
```

```bash
npm run dev --prefix src/asistente.client
```

Abre en `http://localhost:5173`. Usuarios: `cpereyra`, `mlopez` o `jdominguez`; contraseña `demo`.

Vite hace de proxy de `/api` hacia el backend, así que el navegador ve un solo origen y no
hace falta CORS. En producción el frontend compilado se sirve desde la misma aplicación
ASP.NET Core y la situación es idéntica (§11.2).

## Qué es real y qué está simulado

| Área | Hoy | En el sistema final |
|---|---|---|
| Máquina de estados | Real, en el backend. El cliente recibe estado y acciones válidas | Igual |
| Persistencia | Real, EF Core. SQLite en desarrollo, Oracle como destino | Oracle |
| Datos de tickets | Simulados **en el servidor** (`TicketQueryServiceSimulado`) | Vista corporativa read-only tras la misma interfaz |
| Autenticación | Cabecera `X-Usuario-Id` de desarrollo | Cookie ASP.NET Core |
| Excel | Se genera en el navegador con un perfil de columnas provisional | ClosedXML en el backend, sobre la plantilla real |

El cliente ya no calcula transiciones: solo conserva las etiquetas de los botones y un
espejo de la validación de interrupciones, para avisar antes de enviar. El backend la
vuelve a aplicar siempre.

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
