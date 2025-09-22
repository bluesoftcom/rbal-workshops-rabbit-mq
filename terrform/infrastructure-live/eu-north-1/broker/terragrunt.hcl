include "root" {
  path   = find_in_parent_folders("root.hcl")
  expose = true
}

include "env" {
  path = find_in_parent_folders("env.hcl")
  expose = true
}

terraform { source = "../../../terraform-modules/infra/broker" }

inputs = {
  aws_region      = include.env.locals.aws_region
  broker_name     = include.env.locals.broker.broker_name
  engine_version  = include.env.locals.broker.engine_version
  deployment_mode = include.env.locals.broker.deployment_mode
  instance_type   = include.env.locals.broker.instance_type
  subnet_ids      = include.env.locals.broker.subnet_ids
  vpc_id          = include.env.locals.broker.vpc_id
  allowed_cidrs   = include.env.locals.broker.allowed_cidrs
  publicly_accessible = include.env.locals.broker.publicly_accessible

  admin_username  = include.env.locals.broker.admin_username
  admin_password  = include.env.locals.broker.admin_password
}
