-- =====================================================================================
--  Asistente de Registro de Tareas — Índices que hacen cumplir los invariantes
--
--  Ejecutar DESPUÉS de 01-esquema-inicial.sql.
--
--  Estos índices no los genera EF Core: son índices únicos PARCIALES, y el proveedor de
--  Oracle para EF Core no sabe expresarlos. Son la defensa de última línea de §6.1: aunque
--  dos peticiones simultáneas pasen la validación del dominio, la base rechaza la segunda.
--  Sin ellos, un doble clic o dos pestañas en paralelo pueden dejar dos jornadas o dos
--  sesiones abiertas (riesgo alto de §18).
--
--  Técnica: Oracle no indexa las filas donde TODAS las columnas del índice son NULL. Un
--  índice único sobre una expresión CASE que devuelve NULL para las filas que no queremos
--  restringir logra el mismo efecto que un índice filtrado de SQL Server.
--
--  Reemplazar MAOSOL si el esquema es otro.
-- =====================================================================================

-- Invariante: un usuario puede tener como máximo UNA jornada abierta.
-- ESTADO: 0 Pendiente, 1 Activa, 2 EnDescanso, 3 Finalizada.
-- Solo se indexan las jornadas no finalizadas; las cerradas quedan fuera del índice.
CREATE UNIQUE INDEX "MAOSOL"."UX_ASIS_JORNADA_ABIERTA"
    ON "MAOSOL"."ASIS_JORNADA" (CASE WHEN "ESTADO" <> 3 THEN "USUARIO_ID" END);

-- Invariante: una jornada puede tener como máximo UNA sesión abierta.
-- Solo se indexan las sesiones sin cerrar.
CREATE UNIQUE INDEX "MAOSOL"."UX_ASIS_SESION_ABIERTA"
    ON "MAOSOL"."ASIS_SESION" (CASE WHEN "FIN_UTC" IS NULL THEN "JORNADA_ID" END);

-- Verificación rápida: ambas consultas deben devolver cero filas.
--
--   SELECT USUARIO_ID, COUNT(*) FROM MAOSOL.ASIS_JORNADA
--    WHERE ESTADO <> 3 GROUP BY USUARIO_ID HAVING COUNT(*) > 1;
--
--   SELECT JORNADA_ID, COUNT(*) FROM MAOSOL.ASIS_SESION
--    WHERE FIN_UTC IS NULL GROUP BY JORNADA_ID HAVING COUNT(*) > 1;
