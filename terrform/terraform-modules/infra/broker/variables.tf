variable "broker_name" {
  type = string
}

variable "engine_version" {
  type    = string
  default = "3.13"
}

variable "deployment_mode" {
  type    = string
  default = "SINGLE_INSTANCE"
}

variable "instance_type" {
  type    = string
  default = "mq.m5.large"
}

variable "subnet_ids" {
  type = list(string)
}

variable "vpc_id" {
  type = string
}

variable "allowed_cidrs" {
  type = list(string)
}

variable "admin_username" {
  type      = string
  sensitive = true
}

variable "admin_password" {
  type      = string
  sensitive = true
}

variable "publicly_accessible" {
  type    = bool
  default = false
}
