# Elastic indices (rollover)
https://www.elastic.co/guide/en/elasticsearch/reference/current/docs-index_.html
https://www.elastic.co/guide/en/elasticsearch/reference/7.6/getting-started-index-lifecycle-management.html

## Create Policy
```
PUT _ilm/policy/pi-logs
{
  "policy": {
    "phases": {
      "hot": {
        "min_age": "0ms",
        "actions": {
          "rollover": {
            "max_age": "30d",
            "max_primary_shard_size": "50gb"
          }
        }
      },
      "warm": {
        "min_age": "2d",
        "actions": {
          "shrink": {
            "number_of_shards": 1
          },
          "forcemerge": {
            "max_num_segments": 1
          }
        }
      },
      "delete": {
        "min_age": "30d",
        "actions": {
          "delete": {
            "delete_searchable_snapshot": true
          }
        }
      }
    },
    "_meta": {
      "managed": true,
      "description": "ILM policy using the hot and warm phases with a retention of 30 days"
    }
  }
}
PUT /_index_template/staging
{
  "index_patterns" : [
    "staging-*"
  ],
  "priority" : 1,
  "template": {
    "settings" : {
        "lifecycle": {
            "name": "pi-logs",
            "rollover_alias": "staging"
          },
      "number_of_replicas" : 0
    }
  }
}
PUT staging-000001
{
 "aliases": {
 "staging": {
 "is_write_index": true
 }
 }
}
PUT /_index_template/production
{
  "index_patterns" : [
    "production-*"
  ],
  "priority" : 1,
  "template": {
    "settings" : {
        "lifecycle": {
            "name": "pi-logs",
            "rollover_alias": "production"
          },
      "number_of_replicas" : 0
    }
  }
}
PUT production-000001
{
 "aliases": {
 "production": {
 "is_write_index": true
 }
 }
}
```