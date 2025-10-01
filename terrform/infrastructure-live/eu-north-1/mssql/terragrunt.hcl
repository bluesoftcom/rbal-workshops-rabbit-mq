terraform {
  source = "../../../terraform-modules/infra/mssql"
}

include {
  path = find_in_parent_folders("root.hcl")
}

locals {
  env = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  users_min = [
    for u in local.env.locals.users : {
      name     = u.name
      password = u.password
    }
  ]
}

inputs = {
  name                = "mssql-workshop"
  engine_edition      = "sqlserver-ex"
  instance_class      = local.env.locals.mssql.instance
  allocated_storage   = 20
  storage_type        = "gp3"
  multi_az            = false
  publicly_accessible = true

  vpc_id              = local.env.locals.broker.vpc_id
  allowed_cidrs       = local.env.locals.broker.allowed_cidrs

  db_subnet_group_name = null
  subnet_ids           = local.env.locals.broker.subnet_ids

  master_username = local.env.locals.mssql.username
  master_password = local.env.locals.mssql.password

  users = local.users_min

  tags = {
    environment = "workshop"
    region      = local.env.locals.aws_region
    owner       = "platform-team"
  }

  # Bootstrap using sqlcmd available on your laptop/runner
  init_enable    = true
  sql_client_cmd = "sqlcmd"
}
