# /// script
# requires-python = ">=3.9"
# dependencies = [
#   "pika>=1.3.2",
#   "certifi>=2024.2.2"
# ]
# ///
import os, sys, ssl, time, uuid
import pika, certifi

url = os.getenv("RABBIT_URL")
if not url:
    print("RABBIT_URL not set", file=sys.stderr)
    sys.exit(2)

params = pika.URLParameters(url)
if url.startswith("amqps://"):
    ctx = ssl.create_default_context(cafile=certifi.where())
    params.ssl_options = pika.SSLOptions(ctx)
params.heartbeat = 15
params.blocked_connection_timeout = 10
params.socket_timeout = 10
params.connection_attempts = 3
params.retry_delay = 2

conn = pika.BlockingConnection(params)
ch = conn.channel()
queue = "healthcheck"
ch.queue_declare(queue=queue, durable=False, auto_delete=True)
msg = str(uuid.uuid4()).encode()
ch.basic_publish(exchange="", routing_key=queue, body=msg)

deadline = time.time() + 10
body = None
while time.time() < deadline:
    m, h, b = ch.basic_get(queue=queue, auto_ack=True)
    if b:
        body = b
        break
    time.sleep(0.2)

conn.close()
if body == msg:
    print("OK")
    sys.exit(0)
print("FAILED", file=sys.stderr)
sys.exit(1)

