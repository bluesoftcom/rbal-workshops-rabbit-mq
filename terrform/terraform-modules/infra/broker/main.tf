resource "aws_security_group" "mq" {
  count       = var.publicly_accessible ? 0 : 1
  name        = "${var.broker_name}-sg"
  description = "Amazon MQ RabbitMQ"
  vpc_id      = var.vpc_id

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  dynamic "ingress" {
    for_each = var.publicly_accessible ? [] : var.allowed_cidrs
    content {
      description = "HTTPS management"
      from_port   = 443
      to_port     = 443
      protocol    = "tcp"
      cidr_blocks = [ingress.value]
    }
  }

  dynamic "ingress" {
    for_each = var.publicly_accessible ? [] : var.allowed_cidrs
    content {
      description = "AMQPS 5671"
      from_port   = 5671
      to_port     = 5671
      protocol    = "tcp"
      cidr_blocks = [ingress.value]
    }
  }
}

resource "aws_mq_broker" "this" {
  broker_name        = var.broker_name
  engine_type        = "RabbitMQ"
  engine_version     = var.engine_version
  host_instance_type = var.instance_type
  deployment_mode    = "CLUSTER_MULTI_AZ"

  publicly_accessible = var.publicly_accessible
  security_groups     = var.publicly_accessible ? null : [aws_security_group.mq[0].id]

  subnet_ids = var.subnet_ids

  user {
    username       = var.admin_username
    password       = var.admin_password
    console_access = true
  }

  logs {
    general = true
  }

  auto_minor_version_upgrade = true

  maintenance_window_start_time {
    day_of_week = var.maintenance_day
    time_of_day = var.maintenance_time
    time_zone   = var.maintenance_tz
  }

  tags = var.tags
}

data "aws_mq_broker" "this" {
  broker_id = aws_mq_broker.this.id
}

locals {
  console_url   = data.aws_mq_broker.this.instances.0.console_url
  endpoint_host = replace(replace(local.console_url, "https://", ""), "/", "")
}

output "broker_id" {
  value = aws_mq_broker.this.id
}
output "console_url" {
  value = local.console_url
}
output "endpoint_host" {
  value = local.endpoint_host
}
output "wire_endpoints" {
  value = data.aws_mq_broker.this.instances.0.endpoints
}
output "security_group_id" {
  value = var.publicly_accessible ? null : aws_security_group.mq[0].id
}
