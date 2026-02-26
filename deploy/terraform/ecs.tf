module "demo_api_service" {
  source                                          = "git@github.com:zuto/terraform-aws-ecs-service.git?ref=v4.2.0"
  environment                                     = terraform.workspace
  vpc_id                                          = data.aws_vpc.vpc.id
  service_name                                    = "demo-api"
  ecs_cluster_name                                = "ecs-cluster-internal-${terraform.workspace}"
  task_definition_container_definitions           = data.template_file.demo_api_task_definition.rendered
  alarms_sns_topic                                = var.alarm_topic
  task_role_arn                                   = aws_iam_role.demo_api.arn
  task_execution_role                             = aws_iam_role.demo_api.arn
  load_balancer_target_group_deregistration_delay = "30"

  autoscaling_enabled       = true
  autoscaling_minimum_tasks = terraform.workspace == "prod" ? 2 : 1
  autoscaling_policies = [
    {
      metric      = "ECSServiceAverageCPUUtilization"
      targetValue = 70
    }
  ]

  load_balancers = [{
    name                  = "ecs-cluster-internal-${terraform.workspace}-alb2"
    listener_port         = "443"
    target_group_port     = "8080"
    target_group_protocol = "HTTP"
    dns_records = [{
      record_name                  = "demo-api"
      zone_name                    = "${terraform.workspace}.zuto.cloud"
      private_zone                 = true
      create_generic_listener_rule = true
    }]
  }]

  load_balancer_health_check = {
    interval            = 15
    healthy_threshold   = 2
    unhealthy_threshold = 3
    timeout             = 10
    path                = "/health"
    matcher             = 200
  }
}