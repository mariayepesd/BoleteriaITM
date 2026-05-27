/*
 * ITM-Tickets Global — Infraestructura como Código (IaC)
 *
 * Provisionamiento del ecosistema en Kubernetes local (Docker Desktop)
 * o cualquier cluster configurado en ~/.kube/config.
 *
 * Uso:
 *   terraform init
 *   terraform plan  -var="dockerhub_user=tuusuario"
 *   terraform apply -var="dockerhub_user=tuusuario"
 */

terraform {
  required_version = ">= 1.6"
  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.27"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.13"
    }
  }
}

# ─── Providers ────────────────────────────────────────────────────────────────

provider "kubernetes" {
  config_path    = "~/.kube/config"
  config_context = var.kube_context
}

provider "helm" {
  kubernetes {
    config_path    = "~/.kube/config"
    config_context = var.kube_context
  }
}

# ─── Namespace ────────────────────────────────────────────────────────────────

resource "kubernetes_namespace" "festival" {
  metadata {
    name = var.namespace
    labels = {
      "app.kubernetes.io/managed-by" = "terraform"
      "project"                      = "boleteria-itm"
      "env"                          = "production"
    }
  }
}

# ─── Infraestructura de mensajería y caché (Helm) ────────────────────────────

# RabbitMQ — cola de mensajes para el flujo SAGA
resource "helm_release" "rabbitmq" {
  name             = "rabbitmq"
  repository       = "https://charts.bitnami.com/bitnami"
  chart            = "rabbitmq"
  version          = "12.10.0"
  namespace        = kubernetes_namespace.festival.metadata[0].name
  create_namespace = false
  wait             = true
  timeout          = 300

  set { name = "auth.username";   value = var.rabbitmq_user }
  set { name = "auth.password";   value = var.rabbitmq_pass }
  set { name = "replicaCount";    value = "1" }
  set { name = "resources.requests.memory"; value = "256Mi" }
  set { name = "resources.requests.cpu";    value = "100m" }
}

# Redis — caché distribuida para precios dinámicos
resource "helm_release" "redis" {
  name       = "redis"
  repository = "https://charts.bitnami.com/bitnami"
  chart      = "redis"
  version    = "19.0.0"
  namespace  = kubernetes_namespace.festival.metadata[0].name
  wait       = true
  timeout    = 180

  set { name = "auth.enabled";   value = "false" }
  set { name = "architecture";   value = "standalone" }
  set { name = "master.resources.requests.memory"; value = "128Mi" }
  set { name = "master.resources.requests.cpu";    value = "100m" }
}

# Elasticsearch — búsqueda por texto del Search.Api
resource "helm_release" "elasticsearch" {
  name       = "elasticsearch"
  repository = "https://helm.elastic.co"
  chart      = "elasticsearch"
  version    = "8.5.1"
  namespace  = kubernetes_namespace.festival.metadata[0].name
  wait       = true
  timeout    = 600

  set { name = "replicas";                    value = "1" }
  set { name = "minimumMasterNodes";          value = "1" }
  set { name = "xpack.security.enabled";      value = "false" }
  set { name = "resources.requests.memory";   value = "512Mi" }
  set { name = "resources.requests.cpu";      value = "200m" }
  set { name = "esJavaOpts";                  value = "-Xmx256m -Xms256m" }
}

# Qdrant — búsqueda semántica por IA (vectores de embeddings)
resource "helm_release" "qdrant" {
  name       = "qdrant"
  repository = "https://qdrant.github.io/qdrant-helm"
  chart      = "qdrant"
  namespace  = kubernetes_namespace.festival.metadata[0].name
  wait       = true
  timeout    = 180

  set { name = "resources.requests.memory"; value = "256Mi" }
  set { name = "resources.requests.cpu";    value = "100m" }
}

# ─── ConfigMap con URLs internas del cluster ──────────────────────────────────

resource "kubernetes_config_map" "festival_config" {
  metadata {
    name      = "festival-config"
    namespace = kubernetes_namespace.festival.metadata[0].name
  }

  data = {
    ASPNETCORE_ENVIRONMENT     = "Production"
    ServiceUrls__Inventory     = "http://inventory-service:8080"
    ServiceUrls__InventoryGrpc = "http://inventory-service:8080"
    ServiceUrls__Price         = "http://price-service:8080"
    RabbitMQ__Host             = "amqp://${var.rabbitmq_user}:${var.rabbitmq_pass}@rabbitmq:5672"
    Redis__ConnectionString    = "redis-master:6379"
    Elasticsearch__Url         = "http://elasticsearch-master:9200"
    Qdrant__Host               = "qdrant"
    Qdrant__Port               = "6334"
  }

  depends_on = [kubernetes_namespace.festival]
}

# ─── Secret con credenciales de base de datos ────────────────────────────────
# ⚠️  En un entorno real: usar Vault o Azure Key Vault en lugar de Terraform state

resource "kubernetes_secret" "db_credentials" {
  metadata {
    name      = "db-credentials"
    namespace = kubernetes_namespace.festival.metadata[0].name
  }

  type = "Opaque"

  data = {
    inventory-connection = base64encode("Server=sqlserver;Database=FestivalInventoryDb;User Id=sa;Password=${var.sql_password};TrustServerCertificate=True;")
    order-connection     = base64encode("Server=sqlserver;Database=FestivalOrdersDb;User Id=sa;Password=${var.sql_password};TrustServerCertificate=True;")
    price-connection     = base64encode("Server=sqlserver;Database=FestivalPricesDb;User Id=sa;Password=${var.sql_password};TrustServerCertificate=True;")
  }

  depends_on = [kubernetes_namespace.festival]
}
