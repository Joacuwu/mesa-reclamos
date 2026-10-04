# Mesa de Reclamos

[English version](README.en.md)

Sistema para que un centro de atención registre y siga los reclamos de sus clientes: cada reclamo tiene una prioridad con un plazo de resolución (SLA), pasa por estados controlados y deja un historial completo. Los operadores trabajan desde una interfaz web y los otros sistemas de la empresa (por ejemplo, la central telefónica) cargan reclamos por un servicio SOAP.

Hecho con **.NET 10 (ASP.NET Core)**, **Angular 21** y **PostgreSQL**, con la lógica de datos en **stored procedures**.

![Lista de reclamos](docs/capturas/2-lista.png)

## Qué hace

- **Reclamos con SLA:** la prioridad define el plazo (Crítica 4 h, Alta 24 h, Media 48 h, Baja 72 h). La lista marca los vencidos y se puede filtrar por estado, prioridad, texto o solo vencidos, con paginado.
- **Estados controlados:** Abierto → En curso → Esperando al cliente / Resuelto → Cerrado, o Rechazado (con motivo obligatorio). Las transiciones válidas se validan en C# y otra vez en el stored procedure, así la base nunca queda inconsistente.
- **Historial:** cada cambio de estado y cada comentario queda registrado con fecha y usuario.
- **Reporte de SLA** para supervisores: por prioridad, cuántos se resolvieron, cuántos a tiempo, cuántos vencieron y el tiempo promedio.
- **Servicio SOAP** (CoreWCF) con dos operaciones, `AltaReclamo` y `ConsultarReclamo`, con WSDL público y clave en el encabezado `X-Api-Key`. Incluye un cliente de consola en C#.
- **Seguridad:** login con JWT y roles (Operador, Supervisor), contraseñas con PBKDF2, todas las consultas parametrizadas, errores en formato ProblemDetails sin detalles internos.

## Arquitectura

```
web/                    Angular 21: login, lista, detalle, alta y reporte
src/Reclamos.Api        ASP.NET Core: API REST (controllers), servicio SOAP, JWT, manejo de errores
src/Reclamos.Dominio    Entidades y reglas de negocio (POO): Reclamo, Sla, Cuit, ReclamoServicio, interfaces
src/Reclamos.Datos      Repositorios con Npgsql que llaman a stored procedures; scripts SQL en Sql/
src/Reclamos.ClienteSoap  Cliente de consola del servicio SOAP
tests/Reclamos.Tests    42 pruebas con xUnit: dominio, servicio, API completa y SOAP contra PostgreSQL
e2e/recorrido.mjs       Recorrido de la interfaz con Playwright (14 verificaciones)
docs/uml                Diagramas UML (PlantUML): clases, estados, secuencia y modelo de datos
```

La API REST y el servicio SOAP usan la misma clase `ReclamoServicio`, así las dos puertas de entrada aplican las mismas reglas. Los controllers no saben de SQL y el dominio no sabe de HTTP ni de PostgreSQL: depende de interfaces (`IReclamoRepositorio`) que implementa la capa de datos.

| Diagrama de clases | Ciclo de vida |
|---|---|
| ![Clases](docs/uml/clases.png) | ![Estados](docs/uml/estados.png) |

También están el [diagrama de secuencia](docs/uml/secuencia.png) de un cambio de estado y el [modelo de datos](docs/uml/der.png).

## Stored procedures

Están en [src/Reclamos.Datos/Sql/002_procedimientos.sql](src/Reclamos.Datos/Sql/002_procedimientos.sql). Las escrituras son `PROCEDURE` (se llaman con `CALL`) y las lecturas son funciones que devuelven tablas.

| Nombre | Qué hace |
|---|---|
| `sp_reclamo_crear` | Verifica el cliente, arma el número (R-2026-000001), inserta el reclamo y su primera línea de historial. Devuelve el número en un parámetro `INOUT`. |
| `sp_reclamo_cambiar_estado` | Bloquea la fila con `FOR UPDATE`, valida la transición contra la tabla `estado_transiciones`, actualiza y registra el historial. |
| `sp_reclamo_comentar` | Agrega un comentario al historial. |
| `fn_reclamos_buscar` | Búsqueda con filtros y paginado; el total viene en cada fila con `COUNT(*) OVER ()`. |
| `fn_reclamo_obtener`, `fn_reclamo_historial` | Un reclamo y su historial. |
| `fn_reporte_sla` | Reporte de cumplimiento por prioridad. |

Los errores de negocio se levantan con códigos propios (`SQLSTATE 'RC404'` y `'RC422'`) y la capa de datos los traduce a excepciones del dominio, que la API devuelve como 404 y 422.

## Cómo correrlo

Necesitás el SDK de .NET 10, Node.js 22 y PostgreSQL.

1. Crear la base:

   ```sql
   CREATE ROLE reclamos LOGIN PASSWORD 'reclamos';
   CREATE DATABASE reclamos OWNER reclamos;
   ```

2. Compilar la interfaz (una vez):

   ```bash
   cd web
   npm install
   npm run build
   ```

3. Levantar la API, que también sirve la interfaz. En desarrollo crea las tablas, los stored procedures y datos de ejemplo:

   ```bash
   cd src/Reclamos.Api
   dotnet run
   ```

4. Abrir http://localhost:5292 y entrar con `operador` o `supervisor`, contraseña `demo1234`.

La configuración de desarrollo está en `src/Reclamos.Api/appsettings.Development.json`. En producción la cadena de conexión, la clave del JWT y la clave SOAP van en variables de entorno (`ConnectionStrings__Reclamos`, `Jwt__Clave`, `Soap__ApiKey`) y no se cargan datos de ejemplo.

### Servicio SOAP

- WSDL: http://localhost:5292/soap/reclamos?wsdl
- Cliente de consola:

  ```bash
  cd src/Reclamos.ClienteSoap
  dotnet run -- alta 30500011129 Alta "Sin conexión" "No hay servicio en la sucursal Centro"
  dotnet run -- consulta R-2026-000001
  ```

Las respuestas traen un código (00 OK, 04 no existe, 08 dato inválido, 16 error interno) en vez de un SOAP Fault, como suelen hacer los servicios de integración.

## Pruebas

```bash
# Necesita una base vacía para pruebas (se borra y recrea en cada corrida)
psql -c "CREATE DATABASE reclamos_test OWNER reclamos"
dotnet test
```

Las pruebas de integración levantan la API completa con `WebApplicationFactory` contra la base `reclamos_test` (se puede cambiar con la variable `RECLAMOS_TEST_BD`). Cubren, entre otras cosas, el login, los permisos por rol, el ciclo de vida completo, que el stored procedure rechace transiciones aunque se saltee la validación de C#, que dos cambios simultáneos sobre el mismo reclamo no se pisen, un intento de inyección SQL, el cálculo del reporte con fechas controladas y el servicio SOAP con y sin clave.

El recorrido de la interfaz se corre con la API levantada:

```bash
cd e2e
npm install
npx playwright install chromium
node recorrido.mjs
```

## Capturas

| Detalle e historial | Reporte de SLA |
|---|---|
| ![Detalle](docs/capturas/3-detalle.png) | ![Reporte](docs/capturas/5-reporte.png) |

## Notas

- La base es PostgreSQL. Los mismos procedimientos se pueden escribir en SQL Server (T-SQL), Oracle (PL/SQL) o DB2 SQL PL; cambia la sintaxis, no el diseño.
- El servicio SOAP usa CoreWCF, la versión para .NET moderno de WCF. Cumple el mismo rol que un Web Service ASMX de .NET Framework.
