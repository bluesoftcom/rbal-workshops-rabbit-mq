variable "endpoint" {
  type = string
}

variable "admin_username" {
  type      = string
  sensitive = true
}

variable "admin_password" {
  type      = string
  sensitive = true
}

variable "vhost" {
  type    = string
  default = "/"
}

variable "users" {
  type = list(object({
    name      = string
    password  = string
    tags      = optional(list(string), ["management"])
    configure = optional(string, ".*")
    write     = optional(string, ".*")
    read      = optional(string, ".*")
  }))
}
