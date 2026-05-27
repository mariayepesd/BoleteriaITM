variable "kube_context" {
  description = "Contexto de kubectl a usar (docker-desktop, minikube, kind-festival, etc.)"
  type        = string
  default     = "docker-desktop"
}

variable "namespace" {
  description = "Namespace de Kubernetes donde vive el ecosistema"
  type        = string
  default     = "festival"
}

variable "dockerhub_user" {
  description = "Usuario de Docker Hub donde están las imágenes de los microservicios"
  type        = string
}

variable "rabbitmq_user" {
  description = "Usuario de RabbitMQ"
  type        = string
  default     = "guest"
}

variable "rabbitmq_pass" {
  description = "Contraseña de RabbitMQ"
  type        = string
  sensitive   = true
  default     = "guest"
}

variable "sql_password" {
  description = "Contraseña del usuario SA de SQL Server"
  type        = string
  sensitive   = true
  default     = "ITM_Festival_2026!"
}
