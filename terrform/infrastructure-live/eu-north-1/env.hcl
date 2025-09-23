locals {
  aws_region = "eu-north-1"
  broker = {
    broker_name         = "rabbitmq-workshop"
    engine_version      = "3.13"
    deployment_mode     = "SINGLE_INSTANCE"
    instance_type       = "mq.m5.large"
    subnet_ids          = []
    # instance_type       = "mq.t3.micro"
    # subnet_ids          = ["subnet-0e815e41148d3e9ad"]
    vpc_id              = "vpc-090d2de12c7c6e2e9"
    allowed_cidrs       = ["172.31.32.0/32"]
    publicly_accessible = true

    admin_username = "admin"
    admin_password = "79ecbd67-3c9d-487d-aaf4-daa1e8585df0"
  }
  users = [
    {
      name      = "workshop_app",
      password  = "AppP@ssw0rd123!",
      tags      = ["management"],
      configure = ".*",
      write     = ".*",
      read      = ".*"
    },
    {
      name      = "viewer",
      password  = "ViewerP@ssw0rd1!",
      tags      = ["monitoring"],
      configure = "^$",
      write     = "^$",
      read      = "^orders\\..*"
    }
  ]
}
