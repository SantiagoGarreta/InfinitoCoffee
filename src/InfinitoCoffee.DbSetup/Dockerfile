FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY InfinitoCoffee.sln ./
COPY src/InfinitoCoffee.Domain/InfinitoCoffee.Domain.csproj src/InfinitoCoffee.Domain/
COPY src/InfinitoCoffee.Application/InfinitoCoffee.Application.csproj src/InfinitoCoffee.Application/
COPY src/InfinitoCoffee.Infrastructure/InfinitoCoffee.Infrastructure.csproj src/InfinitoCoffee.Infrastructure/
COPY src/InfinitoCoffee.DbSetup/InfinitoCoffee.DbSetup.csproj src/InfinitoCoffee.DbSetup/

RUN dotnet restore src/InfinitoCoffee.DbSetup/InfinitoCoffee.DbSetup.csproj

COPY src ./src

RUN dotnet publish src/InfinitoCoffee.DbSetup/InfinitoCoffee.DbSetup.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "InfinitoCoffee.DbSetup.dll"]
