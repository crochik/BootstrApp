#!/bin/sh
tap-mongodb -c config.json --catalog catalog.json --state state.json 2> log.txt 1> data.json
# tap-mongodb -c config.json --catalog catalog.json --state state.json | target-rabbitmq -c target-rabbitmq.json 