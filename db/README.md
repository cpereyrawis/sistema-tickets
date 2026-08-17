# Scripts de base de datos

Estos scripts **no se ejecutan solos**. El backend no aplica migraciones al arrancar: la
creación de objetos en la base es una decisión de quien administra el esquema, no un
efecto secundario de levantar la aplicación.

## Orden de ejecución

| # | Script | Qué hace |
|---|---|---|
| 1 | `01-esquema-inicial.sql` | Tablas, claves, secuencias, índices y el check de consistencia temporal. Generado por EF Core, idempotente. |
| 2 | `02-indices-invariantes.sql` | Índices únicos parciales que impiden dos jornadas o dos sesiones abiertas. Escrito a mano: EF Core no los expresa en Oracle. |

```bash
sqlplus MAOSOL/MAOSOL@192.168.100.139:1521/ORCLCDB @db/01-esquema-inicial.sql
```

```bash
sqlplus MAOSOL/MAOSOL@192.168.100.139:1521/ORCLCDB @db/02-indices-invariantes.sql
```

## Objetos que se crean

Todos llevan prefijo `ASIS_` para convivir sin ambigüedad con lo que ya exista en el
esquema.

| Tabla | Contenido |
|---|---|
| `ASIS_USUARIO` | Usuarios vinculados a la identidad corporativa. No guarda contraseñas. |
| `ASIS_JORNADA` | Jornadas, con su estado, ticket principal y token de concurrencia. |
| `ASIS_SESION` | Tramos continuos de trabajo. |
| `ASIS_EVENTO` | Bitácora append-only de inicios y fines. |
| `ASIS_AUDITORIA` | Correcciones manuales, append-only. |
| `ASIS_MIGRACIONES` | Historial de migraciones de EF Core. |

## Regenerar el script tras cambiar el modelo

```bash
dotnet ef migrations add NombreDelCambio --project src/Asistente.Persistence --output-dir Database/Migrations
```

```bash
dotnet ef migrations script --idempotent --project src/Asistente.Persistence --output db/01-esquema-inicial.sql
```

El script es idempotente: se puede volver a correr sobre una base que ya tiene parte de
los objetos y solo aplica lo que falta.
