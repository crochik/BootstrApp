```
ssh -L 1433:fcibeta.cfhubs2ytjpi.us-east-2.rds.amazonaws.com:1433 -L 5672:rabbitmq.fci.cloud:5672 -L 9200:ec2-52-15-234-250.us-east-2.compute.amazonaws.com:9200 staging
```