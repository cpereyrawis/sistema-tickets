-- =====================================================================================
--  Asistente de Registro de Tareas — Datos iniciales
--
--  Ejecutar DESPUÉS de 01-esquema.sql y 02-indices-invariantes.sql.
--
--  Carga la nómina y el catálogo de permisos. La aplicación NO da de alta usuarios: no
--  hay registro ni recuperación por correo, así que esta es la única puerta por la que
--  entra una cuenta al sistema. Es deliberado —la nómina es conocida y cerrada— y de paso
--  elimina toda la superficie de un alta automática.
--
--  IDEMPOTENTE: se puede volver a ejecutar sin duplicar nada. No pisa contraseñas ya
--  cambiadas: quien haya elegido la suya la conserva aunque el script vuelva a correr.
--
--  ---------------------------------------------------------------------------------
--  SOBRE LAS CONTRASEÑAS
--
--  Todas las cuentas nacen con la contraseña 24220. Lo que se guarda NO es esa cadena
--  sino su hash PBKDF2-HMAC-SHA512 con sal aleatoria, generado con el mismo componente
--  que usa la aplicación (PasswordHasher de ASP.NET Core Identity).
--
--  Por eso los ocho hashes de abajo son DISTINTOS aunque la contraseña sea la misma: cada
--  uno lleva su propia sal. Esa diferencia es el punto. Si fueran iguales, cualquiera con
--  acceso de lectura a la base sabría de un vistazo qué cuentas comparten contraseña, y
--  descifrar una sola las revelaría todas.
--
--  La operación es unidireccional: de estos valores no se puede volver a 24220. Ni el
--  administrador ni quien consulte la base pueden LEER la contraseña de nadie; a lo sumo
--  pueden ASIGNAR una nueva desde Mantenimiento de Usuarios.
--  ---------------------------------------------------------------------------------
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------------
--  Catálogo de permisos
--
--  Se separan en tres y no en un único "es administrador" porque son atribuciones de
--  riesgo distinto: mirar la nómina no compromete nada, destrabar una cuenta es rutina,
--  y asignar contraseñas es con lo que se suplanta a una persona.
-- ---------------------------------------------------------------------------------
MERGE dbo.T_PERMISO AS destino
USING (VALUES
    ('USUARIO_LISTAR',      'Ver el listado de usuarios y su estado'),
    ('USUARIO_RESET_CLAVE', 'Asignar una contraseña nueva a otro usuario'),
    ('USUARIO_DESBLOQUEAR', 'Levantar el bloqueo por intentos fallidos')
) AS origen (CODIGO, DESCRIPCION)
    ON destino.CODIGO = origen.CODIGO
WHEN NOT MATCHED BY TARGET THEN
    INSERT (CODIGO, DESCRIPCION) VALUES (origen.CODIGO, origen.DESCRIPCION)
WHEN MATCHED THEN
    UPDATE SET DESCRIPCION = origen.DESCRIPCION;
GO

-- ---------------------------------------------------------------------------------
--  Nómina
--
--  El nombre de USUARIO tiene que ser idéntico al del sistema de tickets: es lo único que
--  vincula ambos sistemas y uno inventado no encontraría ningún ticket.
-- ---------------------------------------------------------------------------------
INSERT INTO dbo.T_USUARIO
    (USUARIO, NOMBRE_COMPLETO, CLAVE_HASH, ACTIVO, FECHA_ALTA_UTC,
     ULTIMO_INGRESO_UTC, ULTIMO_CAMBIO_CLAVE_UTC, CANTIDAD_INTENTO_FALLIDO, BLOQUEADO_HASTA_UTC)
SELECT origen.USUARIO, origen.NOMBRE_COMPLETO, origen.CLAVE_HASH, 1, SYSUTCDATETIME(),
       NULL, NULL, 0, NULL
  FROM (VALUES
    ('cpereyra',   'Cristian Pereyra',  'AQAAAAIAAYagAAAAECsurPHyyBREbtAx36XRzG4NIzjaBQcFpLT8lLXn3Yl1LClk7oQZ4pmPD5CKgJW0Yw=='),
    ('mlopez',     'Marina López',      'AQAAAAIAAYagAAAAELDveCB0xAsas2CJVounjTMlvc0tjWnV4Azeh70oH/szG+bl2itP23RrvV04UavR8w=='),
    ('jdominguez', 'Javier Domínguez',  'AQAAAAIAAYagAAAAEBX1M+z3DX0wDeveeEXSxqzip0L97XwkpieYNNcAkUJtn6xLRttiJEOdufeUmkbRlA=='),
    ('rgimenez',   'Rocío Giménez',     'AQAAAAIAAYagAAAAEMuSaSH5zPrZfjY4kf1IaMYkbeQvLp1OlrnGCvM7BsYBDcYXTdy4jnbMf47UbpwNvg=='),
    ('fsosa',      'Federico Sosa',     'AQAAAAIAAYagAAAAEO3ZWqmGk1zk3ZOgGA0OwlZ82igOYNGm96p7/QbKzLRazF58Z656iFK7j5AnTP0+DQ=='),
    ('amartinez',  'Ana Martínez',      'AQAAAAIAAYagAAAAEFC0+kTHv5v2aHEnGeIeCCczD4rC42vZgQFgHj2msoBlWLUHfiBOk8UVbQr8+RG+7A=='),
    ('dvarela',    'Diego Varela',      'AQAAAAIAAYagAAAAEEIzCWTnS7S1IfMyA5kIWJFkKUkWZRNxffHqbIpZmfQjHAAqGqtPXAeUYSaJnK0MQQ=='),
    ('lcastro',    'Lucía Castro',      'AQAAAAIAAYagAAAAEAeyMGsiA24uW034lcnsgT1g6mBSCa2RmoVay1KUn1JkfhPKwePPDzq3Q7HjwCRECQ==')
  ) AS origen (USUARIO, NOMBRE_COMPLETO, CLAVE_HASH)
 WHERE NOT EXISTS (
     SELECT 1 FROM dbo.T_USUARIO u WHERE u.USUARIO = origen.USUARIO
 );
GO

-- ---------------------------------------------------------------------------------
--  Otorgamiento de permisos
--
--  cpereyra es el único con acceso a Mantenimiento de Usuarios. Para sumar a otra
--  persona alcanza con agregar su nombre a la lista de abajo y volver a ejecutar.
-- ---------------------------------------------------------------------------------
INSERT INTO dbo.T_USUARIO_PERMISO (USUARIO_ID, PERMISO_ID, OTORGADO_EN_UTC)
SELECT u.ID, p.ID, SYSUTCDATETIME()
  FROM dbo.T_USUARIO u
 CROSS JOIN dbo.T_PERMISO p
 WHERE u.USUARIO IN ('cpereyra')
   AND p.CODIGO IN ('USUARIO_LISTAR', 'USUARIO_RESET_CLAVE', 'USUARIO_DESBLOQUEAR')
   AND NOT EXISTS (
       SELECT 1
         FROM dbo.T_USUARIO_PERMISO up
        WHERE up.USUARIO_ID = u.ID AND up.PERMISO_ID = p.ID
   );
GO

-- ---------------------------------------------------------------------------------
--  Verificación
-- ---------------------------------------------------------------------------------
--
--   SELECT USUARIO, NOMBRE_COMPLETO, ACTIVO FROM dbo.T_USUARIO ORDER BY USUARIO;
--
--   SELECT u.USUARIO, p.CODIGO
--     FROM dbo.T_USUARIO_PERMISO up
--     JOIN dbo.T_USUARIO u ON u.ID = up.USUARIO_ID
--     JOIN dbo.T_PERMISO p ON p.ID = up.PERMISO_ID
--    ORDER BY u.USUARIO, p.CODIGO;
