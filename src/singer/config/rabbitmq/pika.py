import pika

# Open a connection to RabbitMQ on localhost using all default parameters
url = pika.URLParameters('amqp://host.docker.internal')
connection = pika.BlockingConnection([url])
# 'host.docker.internal', 5672

# Open the channel
channel = connection.channel()

# Declare the queue
channel.queue_declare(queue="pika", durable=True, exclusive=False, auto_delete=False)

# Turn on delivery confirmations
channel.confirm_delivery()

# Send a message
try:
    channel.basic_publish(exchange='ppa',
                          routing_key='test',
                          body='Hello World!',
                          properties=pika.BasicProperties(content_type='text/plain', delivery_mode=1))

    print('Message publish was confirmed')
except pika.exceptions.UnroutableError:
    print('Message could not be confirmed')