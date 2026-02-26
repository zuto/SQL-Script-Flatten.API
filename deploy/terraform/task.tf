data "template_file" "demo_api_task_definition" {
  template = file("task-definition.json.tpl")

  vars = {
    service_name = "demo-api"
    image_name   = "525344447431.dkr.ecr.eu-west-2.amazonaws.com/demo-api:${var.service_version}"
    environment  = terraform.workspace
    cpu          = 256
    memory       = 256
    region       = var.region
  }
}