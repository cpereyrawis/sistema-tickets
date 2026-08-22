# Bases de datos

El sistema usa **dos bases distintas**, y esa separación es deliberada.

| Base | Motor | Acceso | Qué guarda |
|---|---|---|---|
| **Asistente** | SQL Server | Lectura y escritura | Usuarios, permisos, inicios de sesión, jornadas, sesiones, eventos, correcciones y planillas generadas |
| **Tickets** | Oracle | **Solo lectura** | Tickets corporativos. El asistente nunca escribe acá |

No hay claves foráneas entre ambas ni consultas que las mezclen. **El único vínculo es el
nombre de usuario**: `dbo.T_USUARIO.USUARIO` tiene que ser idéntico al del sistema de
tickets, porque es lo que se usa para filtrar los tickets de cada persona.

Esa independencia es lo que permite que una caída de la base corporativa no comprometa lo
ya registrado (NFR-014): la jornada sigue funcionando, solo deja de poder buscar tickets
nuevos.

## Scripts

Se ejecutan a mano y **en orden**. El backend **no crea objetos en la base**: hacerlo sería
un efecto secundario de levantar la aplicación, y quién toca el esquema es decisión de
quien lo administra.

| # | Script | Qué hace |
|---|---|---|
| 1 | `sqlserver/01-esquema.sql` | Tablas, claves, índices y el check de consistencia temporal. Generado por EF Core, idempotente. |
| 2 | `sqlserver/02-indices-invariantes.sql` | Índices únicos filtrados que impiden dos jornadas o dos sesiones abiertas, y la intercalación insensible del nombre de usuario. Escrito a mano. |
| 3 | `sqlserver/03-datos-iniciales.sql` | Catálogo de permisos, la nómina de usuarios y sus otorgamientos. Idempotente: no duplica ni pisa contraseñas ya cambiadas. |

Crear la base una sola vez:

```bash
sqlcmd -S "localhost\SQLEXPRESS" -E -C -Q "IF DB_ID('Asistente') IS NULL CREATE DATABASE Asistente;"
```

Y después los tres scripts:

```bash
sqlcmd -S "localhost\SQLEXPRESS" -E -C -d Asistente -i db/sqlserver/01-esquema.sql
```

```bash
sqlcmd -S "localhost\SQLEXPRESS" -E -C -d Asistente -i db/sqlserver/02-indices-invariantes.sql
```

```bash
sqlcmd -S "localhost\SQLEXPRESS" -E -C -d Asistente -i db/sqlserver/03-datos-iniciales.sql
```

`-E` usa autenticación de Windows y `-C` acepta el certificado autofirmado del servidor
local. Con usuario y contraseña sería `-U sa -P "TuClave"` en lugar de `-E`.

### Volver la nómina a cero

Para recargar los usuarios desde cero y que los IDs vuelvan a arrancar en 1. Borra las
jornadas, sesiones y planillas asociadas, así que solo tiene sentido en desarrollo:

```bash
sqlcmd -S "localhost\SQLEXPRESS" -E -C -d Asistente -Q "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; DELETE FROM dbo.T_USUARIO; DBCC CHECKIDENT('dbo.T_USUARIO', RESEED, 0);"
```

Y después volver a ejecutar el script 3. El `DELETE` necesita `QUOTED_IDENTIFIER ON`
porque la tabla participa de índices filtrados; sin esa opción SQL Server lo rechaza.

> **Los tres archivos están guardados en UTF-8 con BOM, y tiene que seguir siendo así.**
> Sin el BOM, `sqlcmd` los lee con la codificación ANSI del sistema y los acentos entran
> rotos a la base: `Martínez` se convierte en `MartÃ­nez`. Si al editarlos tu editor quita
> el BOM, ejecutalos agregando `-f 65001`.

## Usuarios y contraseñas

La aplicación **no da de alta usuarios**: no hay registro ni recuperación por correo, así
que el script 3 es la única puerta por la que entra una cuenta. Es deliberado —la nómina es
conocida y cerrada— y de paso elimina toda la superficie de un alta automática.

Las ocho cuentas nacen con la contraseña **`24220`**. Para sumar a alguien, se agrega su
fila en el script y se vuelve a ejecutar.

Lo que se guarda en `CLAVE_HASH` **no es la contraseña** sino su hash PBKDF2-HMAC-SHA512
con sal aleatoria, generado con el mismo componente que usa la aplicación. Por eso los ocho
hashes son distintos aunque la contraseña sea la misma: cada uno lleva su propia sal. Si
fueran iguales, cualquiera con acceso de lectura sabría de un vistazo qué cuentas comparten
contraseña.

La operación es unidireccional. **Nadie puede leer la contraseña de nadie** — ni el
administrador ni quien consulte la base. A lo sumo se puede *asignar* una nueva desde
Mantenimiento de Usuarios.

## Permisos

`T_PERMISO` es un catálogo y `T_USUARIO_PERMISO` otorga sus filas a cada usuario. Es una
tabla y no un booleano `ES_ADMIN` porque un booleano obliga a alterar el esquema cada vez
que aparece una atribución nueva, y funde en un solo interruptor cosas que no tienen por
qué ir juntas.

| Código | Habilita |
|---|---|
| `USUARIO_LISTAR` | Ver la nómina y el estado de cada cuenta |
| `USUARIO_RESET_CLAVE` | Asignar una contraseña nueva a otro usuario |
| `USUARIO_DESBLOQUEAR` | Levantar el bloqueo por intentos fallidos |

Los tres los tiene **`cpereyra`**, que es el único con acceso a Mantenimiento de Usuarios.

Están separados porque son de riesgo distinto: mirar la nómina no compromete nada,
destrabar una cuenta es rutina, y asignar contraseñas es con lo que se suplanta a una
persona. La aplicación verifica el permiso **contra la base en cada petición**, no contra
la cookie: quien pierde una atribución la pierde en el momento, no cuando vence su sesión.

## Convención de nombres

| Objeto | Regla | Ejemplo |
|---|---|---|
| Tabla | Prefijo `T_`, MAYÚSCULA, singular | `T_USUARIO` |
| Vista | Prefijo `V_`, MAYÚSCULA, singular | `V_JORNADA_RESUMEN` |
| Columna | MAYÚSCULA, singular, separada con guion bajo | `NOMBRE_COMPLETO` |
| Índice único | `UX_` + tabla + columnas | `UX_T_USUARIO_USUARIO` |
| Índice común | `IX_` + tabla + columnas | `IX_T_JORNADA_USUARIO_FECHA` |
| Restricción | `CK_` + tabla + concepto | `CK_T_SESION_FIN` |

La convención es de la BASE, no del código: en C# las entidades y propiedades siguen
usando PascalCase, que es lo idiomático ahí. La traducción vive en `AsistenteDbContext`,
donde cada propiedad declara su columna con `HasColumnName`.
