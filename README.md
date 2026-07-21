InfinitoCoffee

Sistema web para la gestión de comandas de una cafetería, desarrollado con ASP.NET Core y Angular.

Requisitos

Instalar directamente en Windows:

.NET SDK 10

Node.js 24

npm 11 o compatible

Git

Visual Studio Code

No es necesario instalar Angular CLI globalmente, porque el proyecto utiliza la versión instalada localmente en sus dependencias.

Versiones utilizadas

.NET SDK:      10.0.302
Node.js:       24.18.0
npm:           11.16.0
Angular CLI:   22.0.7
Angular:       22.0.7

Se pueden utilizar versiones de parche posteriores compatibles:

.NET SDK 10.x

Node.js 24.x

npm compatible con Node.js 24

Angular 22.x

1. Instalar .NET SDK 10

Instalar el SDK de .NET 10 desde el sitio oficial de Microsoft.

Después de instalarlo, cerrar y volver a abrir PowerShell y Visual Studio Code.

Verificar:

dotnet --version
dotnet --list-sdks

El resultado debe incluir una versión 10.0.x.

Ejemplo:

10.0.302

2. Instalar Node.js 24

Node.js debe instalarse directamente en Windows.

Ejecutar Node.js mediante Docker no instala Node en el sistema operativo host.

Se puede instalar con Winget:

winget install --id OpenJS.NodeJS.LTS --exact

Después de instalarlo, cerrar todas las terminales y volver a abrir Visual Studio Code.

Verificar:

node --version
npm --version
where.exe node

Resultado esperado:

v24.x.x
C:\Program Files\nodejs\node.exe

3. Clonar el repositorio

git clone <URL_DEL_REPOSITORIO>
cd InfinitoCoffee

Si el repositorio ya está clonado:

git pull

4. Estructura del proyecto

InfinitoCoffee/
  src/
    InfinitoCoffee.Domain/
    InfinitoCoffee.Application/
    InfinitoCoffee.Infrastructure/
    InfinitoCoffee.Api/
    InfinitoCoffee.Frontend/
  tests/
    InfinitoCoffee.Domain.Tests/
    InfinitoCoffee.Application.Tests/
    InfinitoCoffee.Api.IntegrationTests/
  docs/
  InfinitoCoffee.sln
  README.md

5. Restaurar y compilar el backend

Desde la raíz del proyecto:

dotnet restore .\InfinitoCoffee.sln
dotnet build .\InfinitoCoffee.sln

Actualmente puede aparecer una advertencia NU1903 relacionada con Microsoft.OpenApi 2.0.0.

La advertencia no impide compilar ni ejecutar el proyecto y será atendida más adelante.

6. Ejecutar el backend

Desde la raíz:

dotnet run --project .\src\InfinitoCoffee.Api

También se puede ejecutar desde el proyecto de API:

cd .\src\InfinitoCoffee.Api
dotnet run

La terminal mostrará las URLs locales disponibles:

https://localhost:<puerto>
http://localhost:<puerto>

Para confiar en el certificado HTTPS de desarrollo:

dotnet dev-certs https --trust

7. Instalar las dependencias del frontend

Desde la raíz del proyecto:

cd .\src\InfinitoCoffee.Frontend
npm install

No ejecutar nuevamente ng new, porque el workspace Angular ya está creado.

8. Verificar Angular

Dentro de src/InfinitoCoffee.Frontend:

npx ng version

Debe mostrar Angular CLI 22 y Angular 22.

Se utiliza npx ng para ejecutar la versión local del CLI instalada en el proyecto.

9. Compilar el frontend

Dentro de src/InfinitoCoffee.Frontend:

npm run build

La salida se genera en:

src/InfinitoCoffee.Frontend/dist/InfinitoCoffee.Frontend

10. Ejecutar el frontend

Dentro de src/InfinitoCoffee.Frontend:

npm start

Alternativamente:

npx ng serve

Por defecto, Angular estará disponible en:

http://localhost:4200

11. Ejecutar backend y frontend simultáneamente

Abrir dos terminales en Visual Studio Code.

Terminal 1: backend

Desde la raíz:

dotnet run --project .\src\InfinitoCoffee.Api

Terminal 2: frontend

Desde la raíz:

cd .\src\InfinitoCoffee.Frontend
npm start

12. Ejecutar tests

Tests de .NET

Desde la raíz:

dotnet test .\InfinitoCoffee.sln

Tests de Angular

Desde el frontend:

cd .\src\InfinitoCoffee.Frontend
npm test

13. Verificación completa del entorno

Desde la raíz:

dotnet --version
node --version
npm --version
dotnet restore .\InfinitoCoffee.sln
dotnet build .\InfinitoCoffee.sln
dotnet test .\InfinitoCoffee.sln

Luego:

cd .\src\InfinitoCoffee.Frontend
npm install
npx ng version
npm run build

Si todos estos comandos terminan correctamente, el entorno está listo para comenzar a desarrollar.

Resumen rápido

Primera instalación

git clone <URL_DEL_REPOSITORIO>
cd InfinitoCoffee

dotnet restore .\InfinitoCoffee.sln
dotnet build .\InfinitoCoffee.sln

cd .\src\InfinitoCoffee.Frontend
npm install
npm run build

Ejecución diaria

Backend:

dotnet run --project .\src\InfinitoCoffee.Api

Frontend:

cd .\src\InfinitoCoffee.Frontend
npm start