output "namespace" {
  description = "Namespace de Kubernetes creado"
  value       = kubernetes_namespace.festival.metadata[0].name
}

output "gateway_nodeport" {
  description = "Puerto para acceder al API Gateway desde fuera del cluster"
  value       = "localhost:30000"
}

output "rabbitmq_service" {
  description = "Dirección interna de RabbitMQ en el cluster"
  value       = "amqp://${var.rabbitmq_user}:${var.rabbitmq_pass}@rabbitmq:5672"
  sensitive   = true
}

output "redis_service" {
  description = "Dirección interna de Redis en el cluster"
  value       = "redis-master:6379"
}

output "elasticsearch_service" {
  description = "Dirección interna de Elasticsearch en el cluster"
  value       = "http://elasticsearch-master:9200"
}
