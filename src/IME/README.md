# Generate API Client

```
docker run -v "${PWD}:/work" -it nswag openapi2csclient /input:/work/swagger.json /classname:Client /namespace:IME.API /output:/work/API/Client.cs
```