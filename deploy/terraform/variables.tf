variable "hcloud_token" {
  description = "Hetzner Cloud API token"
  type        = string
  sensitive   = true
}

variable "hetznerdns_token" {
  description = "Hetzner DNS API token"
  type        = string
  sensitive   = true
}

variable "ssh_public_key" {
  description = "Public key installed on the server for admin SSH access"
  type        = string
}

variable "admin_cidr" {
  description = "CIDR allowed to reach SSH (22) and the k3s API server (6443)"
  type        = string
}

variable "dns_zone_name" {
  description = "Existing Hetzner DNS zone, e.g. example.com"
  type        = string
}

variable "hostname" {
  description = "Fully-qualified host the app is served on, e.g. finmy.example.com"
  type        = string
}

variable "server_type" {
  description = "Hetzner Cloud server type"
  type        = string
  default     = "cx22"
}

variable "location" {
  description = "Hetzner Cloud datacenter location"
  type        = string
  default     = "nbg1"
}

variable "k3s_version" {
  description = "k3s channel/version passed to the install script"
  type        = string
  default     = "v1.31.4+k3s1"
}

variable "acme_email" {
  description = "Contact email for the Let's Encrypt ClusterIssuer"
  type        = string
}
