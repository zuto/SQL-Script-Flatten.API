variable "alarm_topic" {
  default = "sales-ops-sns-topic"
  type    = string
}

variable "service_version" {
  type = string
}

variable "region" {
  type = string
  default = "eu-west-2"
}