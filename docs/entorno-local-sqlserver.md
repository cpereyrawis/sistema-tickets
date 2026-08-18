# Levantar SQL Server en tu máquina

Necesitás **dos cosas**: el motor y un cliente para consultarlo. Hay tres caminos; elegí
uno.

---

## Opción A — SQL Server Express (recomendada en Windows)

Es la edición gratuita del motor real. Se instala como servicio de Windows y arranca sola
con el equipo.

**Qué instalar**

1. **SQL Server 2022 Express** — [descarga oficial](https://www.microsoft.com/sql-server/sql-server-downloads).
   En la página elegí *Express*. El instalador ofrece tres tipos: usá **Básica**.
2. **SQL Server Management Studio (SSMS)** — [descarga](https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms).
   Es el cliente gráfico. No viene con el motor, se instala aparte.

**Al terminar la instalación** anotá el nombre de la instancia que te muestra. Por defecto
es `localhost\SQLEXPRESS`.

**Habilitar autenticación por usuario y contraseña.** La instalación básica deja solo
autenticación de Windows. Como la aplicación se conecta con usuario y contraseña, hay que
activar el modo mixto:

1. Abrí SSMS y conectate con autenticación de Windows.
2. Clic derecho sobre el servidor → *Properties* → *Security*.
3. Marcá **SQL Server and Windows Authentication mode**.
4. Reiniciá el servicio: *SQL Server Configuration Manager* → *SQL Server Services* →
   clic derecho en la instancia → *Restart*.

**Cadena de conexión**

```
Server=localhost\SQLEXPRESS;Database=Asistente;Trusted_Connection=True;TrustServerCertificate=True
```

Con autenticación de Windows no hace falta usuario ni contraseña, que para desarrollo
local es más simple y más seguro que dejar credenciales escritas.

---

## Opción B — Docker (si ya tenés Docker Desktop)

Más rápido de montar y de tirar abajo, y no deja servicios instalados en el equipo.

```bash
docker run -d --name sqlserver-asistente -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Asistente#2026" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

La contraseña debe tener al menos 8 caracteres con mayúscula, minúscula, número y símbolo,
o el contenedor arranca y muere sin explicación clara.

**Cadena de conexión**

```
Server=localhost,1433;Database=Asistente;User Id=sa;Password=Asistente#2026;TrustServerCertificate=True
```

Para arrancarlo y detenerlo después:

```bash
docker start sqlserver-asistente
```

```bash
docker stop sqlserver-asistente
```

---

## Opción C — LocalDB (la más liviana)

Ya viene con Visual Studio. No es un servicio permanente: arranca cuando algo se conecta.
Sirve para desarrollar, pero **no** se comporta igual que un SQL Server real en
concurrencia, así que no la uses para probar el comportamiento ante dos pestañas
simultáneas.

```bash
sqllocaldb create Asistente -s
```

**Cadena de conexión**

```
Server=(localdb)\Asistente;Database=Asistente;Trusted_Connection=True
```

---

## Crear la base y las tablas

Con cualquiera de las tres opciones, el resto es igual.

**1. Crear la base vacía.** Desde SSMS (`New Query`) o con `sqlcmd`:

```sql
CREATE DATABASE Asistente;
```

**2. Ejecutar los scripts**, en orden:

```bash
sqlcmd -S localhost\SQLEXPRESS -E -d Asistente -i db/sqlserver/01-esquema.sql
```

```bash
sqlcmd -S localhost\SQLEXPRESS -E -d Asistente -i db/sqlserver/02-indices-invariantes.sql
```

El `-E` usa autenticación de Windows. Con Docker sería `-U sa -P "Asistente#2026"` en su
lugar. Si `sqlcmd` no está en el PATH, abrí los dos archivos en SSMS y ejecutalos con F5.

**3. Apuntar el backend.** En `src/Asistente.Api/appsettings.Development.json`:

```json
"Asistente": {
  "Provider": "SqlServer",
  "Schema": "dbo",
  "ConnectionString": "Server=localhost\\SQLEXPRESS;Database=Asistente;Trusted_Connection=True;TrustServerCertificate=True",
  "CommandTimeoutSeconds": 30
}
```

Ojo con la doble barra invertida: en JSON `\` se escribe `\\`.

**4. Verificar** que el backend llega:

```bash
curl http://localhost:5290/api/salud/base
```

Debe responder `{"estado":"conectado"}`.

---

## Cuál elegir

**Docker** si ya lo tenés: es la que se parece más a un servidor real y la que se limpia
sin dejar rastro. **Express** si preferís no depender de Docker y no te molesta instalar un
servicio. **LocalDB** solo si querés empezar en dos minutos y ya tenés Visual Studio.

Mientras tanto, el proyecto sigue funcionando con `"Provider": "Sqlite"` sin instalar
nada, que es como está configurado hoy. SQLite es relacional y respeta transacciones y
constraints, así que sirve para desarrollar; lo que **no** reproduce son los índices
únicos filtrados del script 2, es decir justamente la protección contra dos jornadas
abiertas. Por eso conviene pasar a SQL Server antes de dar por buena esa parte.

---

## Sobre las credenciales

La cadena de conexión de desarrollo puede vivir en `appsettings.Development.json`. Para
cualquier entorno real usá **user-secrets** o variables de entorno: la especificación pide
que ninguna contraseña ni cadena de conexión aparezca en el código (NFR-003, AC-16).

```bash
dotnet user-secrets set "DatabaseSettings:Asistente:ConnectionString" "Server=...;Password=..." --project src/Asistente.Api
```
