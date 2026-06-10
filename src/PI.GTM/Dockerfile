# Build runtime image
FROM microsoft/dotnet:2.1-aspnetcore-runtime-alpine3.7
# FROM microsoft/dotnet:aspnetcore-runtime
# FROM microsoft/dotnet:2.1.5-aspnetcore-runtime-bionic
WORKDIR /app
COPY ./out .
ENTRYPOINT ["dotnet", "PI.GTM.dll"]