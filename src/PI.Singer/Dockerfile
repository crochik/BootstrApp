#FROM crochik/aspnet:8.0-singer-2.0.1
FROM crochik/aspnet:10.0.3-singer    

# app
WORKDIR /app
COPY ./out .
ENTRYPOINT ["dotnet", "PI.Singer.dll"]