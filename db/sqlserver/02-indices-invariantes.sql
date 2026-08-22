-- =====================================================================================
--  Asistente de Registro de Tareas — Índices que hacen cumplir los invariantes
--
--  Ejecutar DESPUÉS de 01-esquema.sql.
--
--  No los genera EF Core: son índices únicos FILTRADOS y el generador de migraciones no
--  los produce. Son la defensa de última línea de §6.1: aunque dos peticiones simultáneas
--  pasen la validación del dominio, la base rechaza la segunda. Sin ellos, un doble clic
--  o dos pestañas en paralelo pueden dejar dos jornadas o dos sesiones abiertas (riesgo
--  alto de §18).
--
--  SQL Server soporta la cláusula WHERE en índices únicos de forma nativa, así que se
--  expresan directamente. (En Oracle habría que recurrir a un índice sobre una expresión
--  CASE que devuelve NULL, porque no indexa filas con todas las columnas nulas.)
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Invariante: un usuario puede tener como máximo UNA jornada abierta.
-- ESTADO: 0 Pendiente, 1 Activa, 2 EnDescanso, 3 Finalizada.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_T_JORNADA_ABIERTA')
BEGIN
    CREATE UNIQUE INDEX UX_T_JORNADA_ABIERTA
        ON dbo.T_JORNADA (USUARIO_ID)
        WHERE ESTADO <> 3;
END
GO

-- Invariante: una jornada puede tener como máximo UNA sesión abierta.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_T_SESION_ABIERTA')
BEGIN
    CREATE UNIQUE INDEX UX_T_SESION_ABIERTA
        ON dbo.T_SESION (JORNADA_ID)
        WHERE FIN_UTC IS NULL;
END
GO

-- ---------------------------------------------------------------------------------
--  Nombre de usuario insensible a mayúsculas
--
--  El nombre de usuario es el ÚNICO vínculo con el sistema de tickets. Si la base se
--  creó con una intercalación sensible a mayúsculas, "cpereyra" y "CPereyra" serían dos
--  cuentas distintas y una de ellas no encontraría sus tickets. Se fuerza la columna a
--  una intercalación insensible para que el índice único ya creado lo impida.
--
--  Es además lo que permite escribir el usuario como salga al iniciar sesión: sin esto,
--  entrar como "CPereyra" sería un usuario inexistente.
-- ---------------------------------------------------------------------------------
IF EXISTS (
    SELECT 1
      FROM sys.columns c
      JOIN sys.tables  t ON t.object_id = c.object_id
     WHERE t.name = 'T_USUARIO'
       AND c.name = 'USUARIO'
       AND c.collation_name IS NOT NULL
       AND c.collation_name NOT LIKE '%[_]CI[_]%'
)
BEGIN
    DROP INDEX UX_T_USUARIO_USUARIO ON dbo.T_USUARIO;

    ALTER TABLE dbo.T_USUARIO
        ALTER COLUMN USUARIO NVARCHAR(64) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL;

    CREATE UNIQUE INDEX UX_T_USUARIO_USUARIO ON dbo.T_USUARIO (USUARIO);
END
GO

-- ---------------------------------------------------------------------------------
--  Verificación: las tres consultas deben devolver cero filas.
-- ---------------------------------------------------------------------------------
--
--   SELECT USUARIO_ID, COUNT(*) FROM dbo.T_JORNADA
--    WHERE ESTADO <> 3 GROUP BY USUARIO_ID HAVING COUNT(*) > 1;
--
--   SELECT JORNADA_ID, COUNT(*) FROM dbo.T_SESION
--    WHERE FIN_UTC IS NULL GROUP BY JORNADA_ID HAVING COUNT(*) > 1;
--
--   SELECT USUARIO, COUNT(*) FROM dbo.T_USUARIO
--    GROUP BY USUARIO HAVING COUNT(*) > 1;
