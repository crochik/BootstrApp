#!/bin/sh
# tap-salesforce -c config.json --properties catalog.json --state state.json 1> output.json 2> extract-log.txt
# cat output.json | target-rabbitmq -c target-rabbitmq.json 2> target-log.txt 1> state.json
# tap-mongodb -c config.json --catalog catalog.json --state state.json | target-csv
# tap-salesforce -c config.json --properties catalog.json 1> output.json 2> log.txt
# tap-salesforce -c config.json --properties catalog-lead.json 1> output-lead.json 2> log-lead.txt
# tap-salesforce -c config.json --properties catalog-contact.json 1> output-contact.json 2> log-contact.txt
# tap-salesforce -c config.json --properties catalog-appointment.json 1> output-appointment.json 2> log-appointment.txt
# tap-salesforce -c config.json --properties catalog-serviceresource.json 1> output-serviceresource.json 2> log-serviceresource.txt
# cat output-lead.json | target-rabbitmq -c target-rabbitmq.json 2> target-lead.txt 1> state-lead.json
# cat output-contact.json | target-rabbitmq -c target-rabbitmq.json 2> target-contact.txt 1> state-contact.json
# cat output-appointment.json | target-rabbitmq -c target-rabbitmq.json 2> target-appointment.txt 1> state-appointment.json
# cat output-user.json | target-rabbitmq -c target-rabbitmq.json 2> target-user.txt 1> state-user.json
# cat output-serviceresource.json | target-rabbitmq -c target-rabbitmq.json 2> target-serviceresource.txt 1> state-serviceresource.json"

# org, user
# cat output.json | target-rabbitmq -c target-rabbitmq.json 2> target.txt 1> state.json
# cat output-lead.json | target-rabbitmq -c target-rabbitmq.json 2> target-lead.txt 1> state-lead.json
cat output-appointment.json | target-rabbitmq -c target-rabbitmq.json 2> target-appointment.txt 1> state-appointment.json