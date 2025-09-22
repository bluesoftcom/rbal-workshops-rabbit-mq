locals {
  default_region = "eu-north-1"
  state_bucket   = "rmq-workshop-tf-state"
  state_table    = "rmq-workshop-tf-locks"
  state_region   = "eu-north-1"
}

remote_state {
  backend = "s3"

  generate = {
    path      = "backend.auto.tf"
    if_exists = "overwrite"
  }

  config = {
    bucket         = local.state_bucket
    key            = "${path_relative_to_include()}/terraform.tfstate"
    region         = local.state_region
    encrypt        = true
    dynamodb_table = local.state_table
  }
}

generate "provider" {
  path      = "providers.auto.tf"
  if_exists = "overwrite"
  contents  = <<EOF
provider "aws" {
  region = "${get_env("AWS_REGION", local.default_region)}"
}
EOF
}
