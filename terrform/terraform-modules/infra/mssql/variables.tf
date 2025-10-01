variable "name" {
  description = "Logical name/prefix for RDS resources"
  type        = string
}

variable "engine_edition" {
  description = "RDS SQL Server edition: sqlserver-ex | sqlserver-web | sqlserver-se | sqlserver-ee"
  type        = string
  default     = "sqlserver-ex"
}

variable "engine_version" {
  description = "Exact engine version (optional). If null, AWS picks a supported default."
  type        = string
  default     = null
}

variable "instance_class" {
  description = "DB instance class. For Express the smallest is db.t3.micro."
  type        = string
  default     = "db.t3.micro"
}

variable "allocated_storage" {
  description = "GiB"
  type        = number
  default     = 20
}

variable "storage_type" {
  description = "gp3 recommended"
  type        = string
  default     = "gp3"
}

variable "multi_az" {
  type    = bool
  default = false
}

variable "publicly_accessible" {
  type    = bool
  default = true
}

variable "vpc_id" {
  type        = string
  description = "VPC id (required when creating SG here)"
}

variable "allowed_cidrs" {
  description = "CIDRs allowed to access 1433"
  type        = list(string)
  default     = []
}

variable "db_subnet_group_name" {
  description = "Existing DB subnet group name. If null and subnet_ids provided, module creates one."
  type        = string
  default     = null
}

variable "subnet_ids" {
  description = "Subnets for DB subnet group (min 2 in different AZs)."
  type        = list(string)
  default     = []
}

variable "master_username" {
  type        = string
  description = "Master SQL login"
}

variable "master_password" {
  type        = string
  sensitive   = true
  description = "Master SQL password"
}

variable "backup_retention_period" {
  type    = number
  default = 0
}

variable "kms_key_id" {
  type        = string
  default     = null
  description = "Optional KMS key id for storage encryption"
}

variable "tags" {
  type    = map(string)
  default = {}
}

variable "users" {
  description = "List of users to provision as { name, password } – DB name == user name"
  type = list(object({
    name     = string
    password = string
  }))
  default = []
}

variable "init_enable" {
  description = "Whether to run SQL bootstrap to create DBs and logins"
  type        = bool
  default     = true
}

variable "sql_client_cmd" {
  description = "Client to run on the machine executing terraform (e.g., sqlcmd)"
  type        = string
  default     = "sqlcmd"
}

variable "port" {
  type    = number
  default = 1433
}
