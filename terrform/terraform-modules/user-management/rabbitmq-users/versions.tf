terraform {
  required_version = ">= 1.5.0"
  required_providers {
    rabbitmq = {
      source  = "cyrilgdn/rabbitmq"
      version = ">= 1.9.0"
    }
  }
}
