# Prepare 

# C#

3 flavors:

https://openapi-generator.tech/docs/generators/csharp

## httpclient
```
docker run --rm -it -w /src -v "${PWD}:/gen" openapitools/openapi-generator-cli generate -i /gen/swagger.json -o /gen/src/httpclient -g csharp -c /gen/csharp-httpclient.json --skip-validate-spec
```

## restsharp
```
docker run --rm -it -w /src -v "${PWD}:/gen" openapitools/openapi-generator-cli generate -i /gen/swagger.json -o /gen/src/restsharp -g csharp -c /gen/csharp-restsharp.json --skip-validate-spec
```

# NSwag

https://github.com/RicoSuter/NSwag/wiki/CommandLine

```
docker run --rm -it  -v "${PWD}:/gen" countingup/nswag openapi2csclient /input:/gen/openapi.yaml /classname:CompanyCamClient /namespace:PI.CompanyCam /output:/gen/Client/CompanyCamClient.cs
```
