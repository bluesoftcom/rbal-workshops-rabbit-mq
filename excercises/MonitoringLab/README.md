# Hands-on Exercise 7: Monitoring & Tracing (RabbitMQ + Prometheus + Grafana)

## 0) File tree
```
MonitoringLab/
  docker-compose.yml
  .env
  config/
    rabbitmq.conf
    enabled_plugins
    definitions.json
  monitoring/
    prometheus.yml
    grafana/
      provisioning/
        datasources/datasource.yaml
        dashboards/dashboard.yaml
      dashboards/rabbitmq-overview.json
```

## 1) Bring up RabbitMQ only
```bash
docker compose up -d
```
Login: http://localhost:15672 admin/admin

## 2) Generate sample traffic
- Verify topology exists:
- Exchanges: ex.orders (topic), orders.retry.ex (topic), orders.error.ex (topic)

### Publish tests in the UI

- Go to Exchanges → click ex.orders.
- Section Publish message:
- Routing key: `order.created`
- Payload (JSON): `{"orderId":"ORD-1","total":120.5}`
- Click Publish message.
- Observe Queues → q.orders counters increase (Ready/Unacked/Acked).
- For retry flow: on Queues → q.orders, click Get messages to peek; use manual Nack/Requeue = false to watch messages traverse q.orders.retry.10s and return after 10s

## 3) Start Prometheus and Grafana
```bash
docker compose up -d
```
Grafana: http://localhost:3000 admin/admin

## 4) Cleanup
```bash
docker compose down -v
```
