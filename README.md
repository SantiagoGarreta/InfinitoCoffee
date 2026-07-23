# InfinitoCoffee

Sistema web para la gestion de comandas de una cafeteria, desarrollado con ASP.NET Core, SQL Server, SignalR y Angular.

## Versiones usadas

- .NET SDK `10.0.302`
- Node.js `24.18.0`
- npm `11.16.0`
- Angular CLI `22.0.7`
- Angular `22.0.7`
- SQL Server en Docker `2022-latest`

## Requisitos del host

Instalar directamente en Windows:

- .NET SDK 10
- Node.js 24
- npm 11 o compatible
- Docker Desktop
- Git

No hace falta instalar Angular CLI globalmente.

## Estructura principal

```text
InfinitoCoffee/
  src/
    InfinitoCoffee.Domain/
    InfinitoCoffee.Application/
    InfinitoCoffee.Infrastructure/
    InfinitoCoffee.Api/
    InfinitoCoffee.Frontend/
    InfinitoCoffee.DbSetup/
  tests/
    InfinitoCoffee.Domain.Tests/
    InfinitoCoffee.Application.Tests/
    InfinitoCoffee.Api.IntegrationTests/
```

## Variables de entorno para Docker

Copiar `.env.example` a `.env` y ajustar si hace falta:

```powershell
Copy-Item .env.example .env
```

Variables principales:

- `SQLSERVER_PORT`
- `SQLSERVER_DATABASE`
- `SQLSERVER_SA_PASSWORD`
- `API_HTTP_PORT`
- `FRONTEND_PORT`
- `FRONTEND_PUBLIC_ORIGIN`
- `API_PUBLIC_BASE_URL`
- `SIGNALR_HUB_PUBLIC_URL`

`.env` queda ignorado por Git.

## Flujo hibrido: SQL Server en Docker y app en host

1. Levantar solo SQL Server:

```powershell
docker compose up -d sqlserver
```

2. Aplicar migraciones explicitamente:

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project .\src\InfinitoCoffee.DbSetup -- migrate
```

3. Cargar seed de desarrollo explicito e idempotente:

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project .\src\InfinitoCoffee.DbSetup -- seed
```

4. Ejecutar backend:

```powershell
dotnet run --project .\src\InfinitoCoffee.Api
```

5. Ejecutar frontend:

```powershell
cd .\src\InfinitoCoffee.Frontend
npm start
```

## Flujo full Docker

1. Revisar la configuracion final:

```powershell
docker compose --env-file .env config
```

2. Construir imagenes:

```powershell
docker compose build
```

3. Levantar solo SQL Server:

```powershell
docker compose up -d sqlserver
```

4. Aplicar migraciones explicitamente:

```powershell
docker compose run --rm migrations
```

5. Ejecutar seed de desarrollo cuando se necesite:

```powershell
docker compose run --rm seed
```

6. Levantar API y frontend:

```powershell
docker compose up -d api frontend
```

7. Verificar servicios:

```powershell
docker compose ps
Invoke-RestMethod http://localhost:5165/health
```

URLs por defecto:

- Frontend: `http://localhost:4200`
- API: `http://localhost:5165`
- Swagger: `http://localhost:5165/swagger`
- Health: `http://localhost:5165/health`
- SignalR Hub: `http://localhost:5165/hubs/orders`

## Estrategia de migraciones y seed

- Las migraciones siguen viviendo en `InfinitoCoffee.Infrastructure`.
- La API no ejecuta `Database.Migrate()` al arrancar.
- El ejecutable `InfinitoCoffee.DbSetup` expone dos comandos explicitos:
  - `migrate`
  - `seed`
- `seed` solo corre si `DOTNET_ENVIRONMENT=Development`.
- El seed es idempotente y solo carga categorias y productos minimos de desarrollo.

## Operaciones utiles de Docker

Ver logs:

```powershell
docker compose logs sqlserver
docker compose logs api
docker compose logs frontend
```

Detener el entorno:

```powershell
docker compose down
```

Eliminar tambien el volumen de SQL Server:

```powershell
docker compose down -v
```

## Troubleshooting rapido

- Si `sqlserver` no llega a healthy, revisar `docker compose logs sqlserver` y validar que la password de `sa` cumpla los requisitos de SQL Server.
- Si `migrations` falla, confirmar que `sqlserver` este healthy antes de reintentar.
- Si el frontend abre pero no carga datos, revisar `http://localhost:5165/health` y luego `docker compose logs api`.
- Si cambias puertos publicos, actualizar `.env` y volver a levantar `frontend` para regenerar `config.js`.

## Backend local

Desde la raiz:

```powershell
dotnet restore .\InfinitoCoffee.sln
dotnet build .\InfinitoCoffee.sln
dotnet test .\InfinitoCoffee.sln
dotnet run --project .\src\InfinitoCoffee.Api
```

Puede aparecer el warning `NU1903` relacionado con OpenAPI. No bloquea el trabajo actual.

## Frontend local

Desde `src/InfinitoCoffee.Frontend`:

```powershell
npm install
npm run build
npm test
```

Para desarrollo:

```powershell
npm start
```

## Configuracion runtime del frontend

- En host, Angular usa los valores por defecto de `src/environments/environment.ts`.
- En Docker, Nginx genera `config.js` al arrancar el contenedor.
- Eso permite cambiar la URL publica de la API y del hub sin recompilar Angular.

## Verificacion sugerida

```powershell
dotnet format .\InfinitoCoffee.sln
dotnet build .\InfinitoCoffee.sln
dotnet test .\InfinitoCoffee.sln

cd .\src\InfinitoCoffee.Frontend
npm install
npm run build
npm test
```
