#!/usr/bin/env python3

import argparse
import io
import os
import sys
import json
import threading
import http.client
import urllib
from datetime import datetime
import collections
import pika

import pkg_resources
from jsonschema.validators import Draft4Validator
import singer

logger = singer.get_logger()

def emit_state(state):
    if state is not None:
        line = json.dumps(state)
        logger.debug('Emitting state {}'.format(line))
        sys.stdout.write("{}\n".format(line))
        sys.stdout.flush()

def flatten(d, parent_key='', sep='__'):
    items = []
    for k, v in d.items():
        new_key = parent_key + sep + k if parent_key else k
        if isinstance(v, collections.MutableMapping):
            items.extend(flatten(v, new_key, sep=sep).items())
        else:
            items.append((new_key, str(v) if type(v) is list else v))
    return dict(items)

def remove_nulls(d):
    return {k: v for k, v in d.items() if v is not None}

def publish_message(channel, routing_key, message):
    channel.basic_publish(exchange=exchange,
        routing_key=routing_key,
        body=json.dumps(remove_nulls(message), indent=4),
        properties=pika.BasicProperties(content_type='text/json'))
        # , delivery_mode=1

def persist_lines(config, lines):
    global channel
    global exchange
    global routing_key

    state = None
    schemas = {}
    key_properties = {}
    headers = {}
    validators = {}
    
    now = datetime.now().strftime('%Y%m%dT%H%M%S')

    # Loop over lines from stdin
    for line in lines:
        try:
            o = json.loads(line)
        except json.decoder.JSONDecodeError:
            logger.error("Unable to parse:\n{}".format(line))
            raise

        if 'type' not in o:
            raise Exception("Line is missing required key 'type': {}".format(line))
        t = o['type']

        if t == 'RECORD':
            if 'stream' not in o:
                raise Exception("Line is missing required key 'stream': {}".format(line))
            if o['stream'] not in schemas:
                raise Exception("A record for stream {} was encountered before a corresponding schema".format(o['stream']))

            # Get schema for this record's stream
            schema = schemas[o['stream']]

            # Validate record
            validators[o['stream']].validate(o['record'])

            # publish
            route = routing_key.format('record', o['stream'])
            publish_message(channel, route, o['record'])
            state = None

        elif t == 'STATE':
            logger.debug('Setting state to {}'.format(o['value']))
            state = o['value']

            # publish
            route = routing_key.format('target', 'state')
            publish_message(channel, route, o['value'])

        elif t == 'SCHEMA':
            if 'stream' not in o:
                raise Exception("Line is missing required key 'stream': {}".format(line))
            stream = o['stream']
            schemas[stream] = o['schema']
            validators[stream] = Draft4Validator(o['schema'])
            if 'key_properties' not in o:
                raise Exception("key_properties field is required")
            key_properties[stream] = o['key_properties']
            
            # publish
            route = routing_key.format('schema', o['stream'])
            publish_message(channel, route, o['schema'])

        elif t == 'ACTIVATE_VERSION':
            stream = o['stream']

            # publish
            route = routing_key.format('activate', o['stream'])
            publish_message(channel, route, o)

        else:
            raise Exception("Unknown message type {} in message {}"
                            .format(o['type'], o))
    
    return state


def send_usage_stats():
    try:
        version = pkg_resources.get_distribution('target-csv').version
        conn = http.client.HTTPConnection('collector.singer.io', timeout=10)
        conn.connect()
        params = {
            'e': 'se',
            'aid': 'singer',
            'se_ca': 'target-rabbitmq',
            'se_ac': 'open',
            'se_la': version,
        }
        conn.request('GET', '/i?' + urllib.parse.urlencode(params))
        response = conn.getresponse()
        conn.close()
    except:
        logger.debug('Collection request failed')

def connect(connectionString):
    url = pika.URLParameters(connectionString)
    connection = pika.BlockingConnection([url])
    channel = connection.channel()
    channel.confirm_delivery()
    return channel

def main():
    global channel
    global exchange
    global routing_key

    parser = argparse.ArgumentParser()
    parser.add_argument('-c', '--config', help='Config file')
    args = parser.parse_args()

    if args.config:
        with open(args.config) as input:
            config = json.load(input)
    else:
        raise Exception('Missing configuration parameter')

    # config
    url = config.get('url')
    exchange = config.get('exchange')
    routing_key = config.get('routing_key')
    if not exchange:
        raise Exception('Invalid exchange configuration')
    if not url:
        raise Exception('Invalid url configuration')
    if not routing_key:
        raise Exception('Invalid routing_key configuration')

    if not config.get('disable_collection', False):
        logger.info('Sending version information to singer.io. ' +
                    'To disable sending anonymous usage data, set ' +
                    'the config parameter "disable_collection" to true')
        threading.Thread(target=send_usage_stats).start()

    # connect
    channel = connect(url)
    
    input = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8')
    state = persist_lines(config, input)
        
    emit_state(state)
    logger.debug("Exiting normally")


if __name__ == '__main__':
    main()
