resource "hcloud_ssh_key" "admin" {
  name       = "finmy-admin"
  public_key = var.ssh_public_key
}

resource "hcloud_firewall" "finmy" {
  name = "finmy"

  rule {
    direction  = "in"
    protocol   = "tcp"
    port       = "22"
    source_ips = [var.admin_cidr]
  }

  rule {
    direction  = "in"
    protocol   = "tcp"
    port       = "6443"
    source_ips = [var.admin_cidr]
  }

  rule {
    direction  = "in"
    protocol   = "tcp"
    port       = "80"
    source_ips = ["0.0.0.0/0", "::/0"]
  }

  rule {
    direction  = "in"
    protocol   = "tcp"
    port       = "443"
    source_ips = ["0.0.0.0/0", "::/0"]
  }
}

resource "hcloud_server" "finmy" {
  name         = "finmy"
  server_type  = var.server_type
  location     = var.location
  image        = "ubuntu-24.04"
  ssh_keys     = [hcloud_ssh_key.admin.id]
  firewall_ids = [hcloud_firewall.finmy.id]

  user_data = templatefile("${path.module}/cloud-init.yaml", {
    k3s_version = var.k3s_version
    fqdn        = var.hostname
    acme_email  = var.acme_email
  })
}

data "hetznerdns_zone" "finmy" {
  name = var.dns_zone_name
}

resource "hetznerdns_record" "finmy_a" {
  zone_id = data.hetznerdns_zone.finmy.id
  name    = trimsuffix(var.hostname, ".${var.dns_zone_name}")
  type    = "A"
  value   = hcloud_server.finmy.ipv4_address
  ttl     = 300
}

resource "hetznerdns_record" "finmy_aaaa" {
  zone_id = data.hetznerdns_zone.finmy.id
  name    = trimsuffix(var.hostname, ".${var.dns_zone_name}")
  type    = "AAAA"
  value   = hcloud_server.finmy.ipv6_address
  ttl     = 300
}
