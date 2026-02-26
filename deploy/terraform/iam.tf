resource "aws_iam_role" "demo_api" {
  name               = "demo-api-iam-role"
  description        = "The IAM instance role that the demo-api uses"
  assume_role_policy = data.aws_iam_policy_document.ecs_policy.json
}

resource "aws_iam_role_policy_attachment" "attach_ecs_execution_policy" {
  role       = aws_iam_role.demo_api.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}


resource "aws_iam_role_policy" "parameter_store_access_policy" {
  name = "${local.name}-parameter-store-access-policy"
  role = aws_iam_role.demo_api.id
  policy = data.aws_iam_policy_document.parameter_store_policy.json
}

data "aws_iam_policy_document" "parameter_store_policy" {
  version = "2012-10-17"
  statement {
    sid    = "IAMRoleParameterStoreAccess"
    effect = "Allow"
    actions = [
      "ssm:GetParameters"
    ]
    resources = [
      "arn:aws:ssm:*:*:parameter/database/horizon/horizon-user",
      "arn:aws:ssm:*:*:parameter/services/experiment-service/*",
      "arn:aws:ssm:*:*:parameter/services/logstash/default-uri"
    ]
  }
}

data "aws_iam_policy_document" "ecs_policy" {
  version = "2012-10-17"
  statement {
    actions = ["sts:AssumeRole"]
    effect  = "Allow"
    principals {
      identifiers = ["ecs-tasks.amazonaws.com"]
      type        = "Service"
    }
  }
}