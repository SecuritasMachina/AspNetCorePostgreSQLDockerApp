

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0
# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

#RUN mkdir -p /mnt/offsite
# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "SecuritasMachinaOffsiteAgent.dll"]

#docker build -t securitasmachinaoffsiteagent .
#docker tag securitasmachinaoffsiteagent  securitasmachina2022/securitasmachinaoffsiteagent:latest
#docker push securitasmachina2022/securitasmachinaoffsiteagent:latest