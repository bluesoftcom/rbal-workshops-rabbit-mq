provider "rabbitmq" {
  endpoint = "https://${var.endpoint}"
  username = var.admin_username
  password = var.admin_password
}

resource "rabbitmq_vhost" "this" {
  name = var.vhost
}

locals {
  users_map = { for u in var.users : u.name => u }
}

resource "rabbitmq_user" "users" {
  for_each = local.users_map
  name     = each.value.name
  password = each.value.password
  tags     = try(each.value.tags, ["management"])
}

resource "rabbitmq_vhost" "per_user" {
  for_each = local.users_map
  name     = each.value.name
}

resource "rabbitmq_permissions" "per_user_full" {
  for_each = local.users_map
  user  = rabbitmq_user.users[each.key].name
  vhost = rabbitmq_vhost.per_user[each.key].name
  permissions {
    configure = ".*"
    write     = ".*"
    read      = ".*"
  }
}

resource "rabbitmq_permissions" "perms" {
  for_each = local.users_map
  user  = rabbitmq_user.users[each.key].name
  vhost = rabbitmq_vhost.this.name
  permissions {
    configure = try(each.value.configure, ".*")
    write     = try(each.value.write, ".*")
    read      = try(each.value.read, ".*")
  }
}

output "created_users" {
  value = keys(local.users_map)
}

output "vhost" {
  value = rabbitmq_vhost.this.name
}

output "per_user_vhosts" {
  value = [for k, v in rabbitmq_vhost.per_user : v.name]
}
