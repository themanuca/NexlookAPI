FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar arquivos de projeto para restaurar dependências primeiro (cache)
COPY NexLookSolution/NexLookSolution.sln ./NexLookSolution/
COPY NexLookSolution/NexlookAPI/NexlookAPI.csproj ./NexLookSolution/NexlookAPI/
COPY NexLookSolution/Application/Application.csproj ./NexLookSolution/Application/
COPY NexLookSolution/Domain/Domain.csproj ./NexLookSolution/Domain/
COPY NexLookSolution/Infra/Infra.csproj ./NexLookSolution/Infra/

RUN dotnet restore ./NexLookSolution/NexLookSolution.sln

# Copiar todo o código fonte e publicar
COPY NexLookSolution/ ./NexLookSolution/
RUN dotnet publish ./NexLookSolution/NexlookAPI/NexlookAPI.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 8080

ENTRYPOINT ["/entrypoint.sh"]
