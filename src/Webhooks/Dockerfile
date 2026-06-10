FROM mcr.microsoft.com/dotnet/aspnet:10.0.2

# app
WORKDIR /app
COPY ./out .
ENTRYPOINT ["dotnet", "Webhooks.dll"]
