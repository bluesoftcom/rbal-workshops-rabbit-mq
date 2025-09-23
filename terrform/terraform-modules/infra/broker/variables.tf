variable "aws_region" {
  type        = string
  description = "AWS region"
}

variable "broker_name" {
  type        = string
  description = "Broker name"
}

variable "engine_version" {
  type        = string
  description = "RabbitMQ engine version e.g. 3.13.6"
  default     = "3.13.6"
}

variable "instance_type" {
  type        = string
  description = "Host instance type; for cluster use mq.m5.large or bigger"
  default     = "mq.m5.large"
}

variable "publicly_accessible" {
  type        = bool
  description = "Expose endpoints publicly"
  default     = false
}

variable "vpc_id" {
  type        = string
  description = "VPC ID"
}

variable "subnet_ids" {
  type        = list(string)
  description = "Subnets (prefer 3 in distinct AZs for cluster)"
}

variable "allowed_cidrs" {
  type        = list(string)
  description = "CIDRs allowed to access when private"
  default     = []
}

variable "admin_username" {
  type        = string
  description = "Initial admin username"
}

variable "admin_password" {
  type        = string
  description = "Initial admin password"
  sensitive   = true
}

variable "maintenance_day" {
  type        = string
  default     = "SUNDAY"
}

variable "maintenance_time" {
  type        = string
  default     = "02:00"
}

variable "maintenance_tz" {
  type        = string
  default     = "UTC"
}

variable "tags" {
  type        = map(string)
  default     = {}
}
