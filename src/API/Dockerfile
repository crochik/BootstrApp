## 1. Use the NATIVE SDK for the host (arm64) to avoid emulation
#FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
#ARG TARGETARCH
#WORKDIR /src
#
## 2. Restore using the target architecture flag
## This downloads the correct amd64 native dependencies using arm64 speed
#COPY ["API.csproj", "./"]
#RUN dotnet restore -a $TARGETARCH
#
## 3. Build and Publish
#COPY . .
#RUN dotnet publish "API.csproj" -c Release -o /app/publish -a $TARGETARCH --no-restore
#
## 4. Final stage uses the actual target runtime (amd64)
#FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
#WORKDIR /app
#COPY --from=build /app/publish .
#ENTRYPOINT ["dotnet", "API.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY ./out .
ENTRYPOINT ["dotnet", "API.dll"]