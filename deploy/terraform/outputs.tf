output "server_ipv4" {
  value = hcloud_server.finmy.ipv4_address
}

output "server_ipv6" {
  value = hcloud_server.finmy.ipv6_address
}

output "fqdn" {
  value = var.hostname
}
