terraform {
  required_version = ">= 1.9"

  required_providers {
    hcloud = {
      source  = "hetznercloud/hcloud"
      version = "~> 1.50"
    }
    hetznerdns = {
      source  = "germanbrew/hetznerdns"
      version = "~> 3.5"
    }
  }

  # No backend block: this configuration has never been applied (see
  # deploy/terraform/README.md). Pick a real backend before the first apply
  # rather than defaulting to local state on whoever's machine runs it first.
}

provider "hcloud" {
  token = var.hcloud_token
}

provider "hetznerdns" {
  api_token = var.hetznerdns_token
}
