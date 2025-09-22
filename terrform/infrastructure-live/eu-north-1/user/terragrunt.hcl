include "root" {
  path   = find_in_parent_folders("root.hcl")
  expose = true
}

include "env" {
  path = find_in_parent_folders("env.hcl")
  expose = true
}

dependency "broker" {
  config_path = "../broker"
  mock_outputs = {
    endpoint_host = "mock-endpoint"
    broker_output = "mock-broker-output"
  }
  mock_outputs_allowed_terraform_commands = ["init", "validate", "plan"]
}

terraform { source = "../../../terraform-modules/user-management/rabbitmq-users" }
inputs = {
  endpoint       = dependency.broker.outputs.endpoint_host
  admin_username = include.env.locals.broker.admin_username
  admin_password = include.env.locals.broker.admin_password
  port           = 443
  vhost          = "/"
  users          = include.env.locals.users
}
