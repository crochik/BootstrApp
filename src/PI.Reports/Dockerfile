FROM mcr.microsoft.com/dotnet/aspnet:10.0.2
WORKDIR /app

# Devexpress
RUN apt-get update && \
    apt-get install -y \
    libgdiplus libc6-dev \
    libicu-dev libharfbuzz0b libfontconfig1 libfreetype6 && \ 
    rm -rf /var/lib/apt/lists/*

COPY ./out .

ENTRYPOINT ["dotnet", "PI.Reports.dll"]