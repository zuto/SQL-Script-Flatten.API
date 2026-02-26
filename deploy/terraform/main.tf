terraform {
  required_version = ">=1.6.6"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  backend "s3" {
    bucket = "zuto-terraform-state-files"
    key    = "services/demo-api/demo-api.tfstate"
    region = "eu-west-2"
    acl    = "bucket-owner-full-control"
  }
}

provider "aws" {
  region = var.region

  default_tags {
    tags = {
      "zuto:operations:domain" : "Customer",
      "zuto:operations:source" : "https://github.com/zuto/demo-api",
      "zuto:operations:provisioner" : "Terraform",
      "zuto:cost-allocation:system" : "",
      "zuto:cost-allocation:sub-system" : "",
    }
  }
}

provider "template" {}

locals {
  name = "demo-api"
}

data "aws_vpc" "vpc" {
  filter {
    name   = "tag:Name"
    values = ["vpc.eu-west-2"]
  }
}