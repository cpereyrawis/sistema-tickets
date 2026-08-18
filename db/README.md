# Bases de datos

El sistema usa **dos bases distintas**, y esa separación es deliberada.

| Base | Motor | Acceso | Qué guarda |
|---|---|---|---|
| **Asistente** | SQL Server | Lectura y escritura | Usuarios, inicios de sesión, jornadas, sesiones, eventos, correcciones y planillas generadas |
| **Tickets** | Oracle | **Solo lectura** | Tickets corporativos. El asistente nunca escribe acá |

No hay claves foráneas entre ambas ni consultas que las mezclen. **El único vínculo es el
nombre de usuario**: `dbo.T_USUARIO.USUARIO` tiene que ser idéntico al del sistema de
tickets, porque es lo que se usa para filtrar los tickets de cada persona.

Esa independencia es lo que permite que una caída de la base corporativa no comprometa lo
ya registrado (NFR-014): la jornada sigue funcionando, solo deja de poder buscar tickets
nuevos.

## Scripts

Se ejecutan a mano. El backend **no crea objetos en la base**: hacerlo sería un efecto
secundario de levantar la aplicación, y quién toca el esquema es decisión de quien lo
administra.

| # | Script | Qué hace |
|---|---|---|
| 1 | `sqlserver/01-esquema.sql` | Tablas, claves, índices y el check de consistencia temporal. Generado por EF Core, idempotente. |
| 2 | `sqlserver/02-indices-invariantes.sql` | Índices únicos filtrados que impiden dos jornadas o dos sesiones abiertas, y la intercalación insensible del nombre de usuario. Escrito a mano. |

```bash
sqlcmd -S localhost -U sa -P "TuClave" -d Asistente -i db/sqlserver/01-esquema.sql
```

```bash
sqlcmd -S localhost -U sa -P "TuClave" -d Asistente -i db/sqlserver/02-indices-invariantes.sql
```

## Convención de nombres

| Objeto | Regla | Ejemplo |
|---|---|---|
| Tabla | Prefijo `T_`, MAYÚSCULA, singular | `T_USUARIO` |
| Vista | Prefijo `V_`, MAYÚSCULA, singular | `V_JORNADA_RESUMEN` |
| Columna | MAYÚSCULA, singular, separada con guion bajo | `NOMBRE_COMPLETO` |
| Índice único | `UX_` + tabla + columnas | `UX_T_USUARIO_EMAIL` |
| Índice común | `IX_` + tabla + columnas | `IX_T_JORNADA_USUARIO_FECHA` |
| Restricción | `CK_` + tabla + concepto | `CK_T_SESION_FIN` |

La convención es de la BASE, no del código: en C# las entidades y propiedades siguen
usando PascalCase, que es lo idiomático ahí. La traducción vive en `AsistenteDbContext`,
donde cada propiedad declara su columna con `HasColumnName`.

Dos columnas son contadores y no entidades, así que se nombran como cantidad para poder
mantener el sustantivo en singular sin que el nombre mienta:
`T_USUARIO.CANTIDAD_INTENTO_FALLIDO` y `T_PLANILLA.CANTIDAD_FILA`.

Todavía no hay vistas. Cuando aparezcan, el prefijo `V_` las distingue de las tablas.

## Tablas de la base del asistente

| Tabla | Contenido |
|---|---|
| `T_USUARIO` | Cuenta de acceso. `USUARIO` es el vínculo con el sistema de tickets; `CLAVE_HASH` guarda solo el hash, nunca la contraseña. Incluye contador de intentos fallidos y bloqueo temporal. |
| `T_SESION_USUARIO` | Bitácora de accesos: quién entró, cuándo, desde qué IP y cómo terminó. No es el mecanismo de sesión, que lo sostiene la cookie. |
| `T_TOKEN_USUARIO` | Tokens de un solo uso para activar la cuenta y restablecer la contraseña. Guarda el hash del token, nunca el valor que viajó por correo. |
| `T_JORNADA` | Una por día y usuario. Estado, ticket principal y token de concurrencia. |
| `T_SESION` | Tramos continuos de trabajo, con una copia mínima del ticket para no depender de la base corporativa al leer. |
| `T_EVENTO` | Bitácora append-only de inicios y fines. Los cuatro de una interrupción comparten `CORRELACION_ID`. |
| `T_AUDITORIA` | Correcciones manuales, append-only. |
| `T_PLANILLA` | Cada Excel generado: metadatos, hash y —opcionalmente— el archivo. `NUMERO_GENERACION` distingue la original de las regeneraciones. |
| `T_MIGRACION` | Historial de EF Core. |

## Por qué se guarda el archivo de la planilla

`T_PLANILLA.CONTENIDO` conserva los bytes del `.xlsx`. El importador corporativo recibe ese
archivo exacto; si más adelante aparece una discrepancia, poder recuperar el que
realmente se entregó es la única forma de dirimirla. El hash permite verificar que lo
almacenado es lo que se descargó.

Si el volumen molesta, la columna admite NULL: se puede dejar de guardar el binario sin
tocar el esquema, conservando los metadatos.

## Regenerar el script tras cambiar el modelo

```bash
dotnet ef migrations add NombreDelCambio --project src/Asistente.Persistence --output-dir Database/Migrations
```

```bash
dotnet ef migrations script --idempotent --project src/Asistente.Persistence --output db/sqlserver/01-esquema.sql
```

El script es idempotente: se puede volver a correr sobre una base que ya tiene parte de
los objetos y solo aplica lo que falta. El script 2 hay que mantenerlo a mano.

## Base de tickets

No se crea nada. Solo hace falta:

1. Una **vista** que exponga los tickets con las columnas que el asistente necesita.
2. Una **cuenta con permiso SELECT** exclusivamente sobre esa vista.

Los nombres de la vista y de sus columnas son configurables en `appsettings.json`, sección
`DatabaseSettings:Tickets:Mapeo`, porque el esquema real todavía no se relevó. Cuando se
conozca, apuntar el adaptador es cambiar configuración y no recompilar.

La columna que vincula ambos sistemas es la configurada en `ColumnaAsignadoA`: debe
contener el **nombre de usuario**, no el nombre y apellido de la persona.
