# Imagem OPCIONAL do NUCLAUD. A aplicação também roda sem Docker (ver docs/IMPLANTACAO.md).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/NuClaud.Web -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    DataProtection__CaminhoChaves=/dados/chaves
VOLUME ["/dados"]
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "NuClaud.Web.dll"]
