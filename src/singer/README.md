# Generate catalog
https://github.com/singer-io/tap-mongodb
```
docker-compose run singer tap-mongodb --config /config/config.json --discover > config/catalog.json
```

```
tap-mongodb -c config.json --catalog catalog.json > output.json
```

# Select
```
            "selected": true,
            "replication-method": "FULL_TABLE",
            "tap-mongodb.projection": "{\"Name\": \"1\"}"
```