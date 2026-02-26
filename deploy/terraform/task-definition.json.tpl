[
  {
    "name": "${service_name}",
    "image": "${image_name}",
    "cpu": ${cpu},
    "memory": ${memory},
    "essential": true,
    "portMappings": [
      {
        "containerPort": 8080
      }
    ],
    "environment": [
      {
        "name": "ASPNETCORE_ENVIRONMENT",
        "value": "${environment}"
      },
      {
        "name": "AWS_REGION",
        "value": "${region}"
      }
    ],
    "secrets": [
      {
        "name": "ConnectionStrings__Horizon",
        "valueFrom": "/database/horizon/horizon-user"
      },
      {
        "name": "ExperimentService__BaseUrl",
        "valueFrom": "/services/experiment-service/base-uri"
      },
      {
        "name": "ExperimentService:Key",
        "valueFrom": "/services/experiment-service/key"
      },
      {
        "name": "LogstashUrl",
        "valueFrom": "/services/logstash/default-uri"
      }
    ]
  }
]