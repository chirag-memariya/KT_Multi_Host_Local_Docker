# Linux Local Multi-Host Deployment for NVR1Service

This folder contains a simple Linux-only local deployment for the existing `Demo/NVR1Service` application.

This version is currently configured with a single Linux node.

What this deployment does:

- installs .NET 8 SDK on each target Linux host
- copies the existing `Demo/NVR1Service` source to each host
- publishes the service on each host
- runs the service with `systemd`

Assumptions:

- target hosts are Ubuntu or Debian based Linux machines
- Ansible can connect to the target hosts with SSH
- `RABBITMQ_HOST` is reachable from each Linux host

Files:

- `inventory.ini` example inventory for Linux hosts
- `deploy-local.yml` Ansible playbook for local multi-host deployment
- `templates/nvr1service.service.j2` systemd unit template

Usage:

```bash
cd "Multi-Host Deployment KT/Linux-Local-NVR1Service"
ansible-playbook -i inventory.ini deploy-local.yml
```

Before running:

- update hostnames, SSH user, and private key path in `inventory.ini`
- update `rabbitmq_host` inside `deploy-local.yml` if needed
- ensure the control machine runs the playbook from inside this folder so `../Demo/NVR1Service` resolves correctly
