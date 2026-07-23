FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY InfinitoCoffee.sln ./
COPY src/InfinitoCoffee.Domain/InfinitoCoffee.Domain.csproj src/InfinitoCoffee.Domain/
COPY src/InfinitoCoffee.Application/InfinitoCoffee.Application.csproj src/InfinitoCoffee.Application/
COPY src/InfinitoCoffee.Infrastructure/InfinitoCoffee.Infrastructure.csproj src/InfinitoCoffee.Infrastructure/
COPY src/InfinitoCoffee.Api/InfinitoCoffee.Api.csproj src/InfinitoCoffee.Api/

RUN dotnet restore src/InfinitoCoffee.Api/InfinitoCoffee.Api.csproj

COPY src ./src

RUN dotnet publish src/InfinitoCoffee.Api/InfinitoCoffee.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "InfinitoCoffee.Api.dll"]
