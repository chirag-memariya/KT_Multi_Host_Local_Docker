# Linux Docker Swarm Deployment for NVR1Service

This folder contains a simple Linux-only Docker Swarm deployment for the existing `Demo/NVR1Service` application.

This version is currently configured as a single-node swarm.

What this deployment does:

- initializes a swarm on the first manager node
- copies the existing `Demo/NVR1Service` source to the manager
- builds a local Docker image on the manager
- deploys a single-service stack for `NVR1Service`

Assumptions:

- Docker Engine is already installed on the Linux node
- the service will run on the manager node only to avoid needing a registry
- `RABBITMQ_HOST` is reachable from the manager node

Files:

- `inventory.ini` example inventory for a single swarm manager node
- `deploy-swarm.yml` Ansible playbook for swarm bootstrap and deployment
- `stack/docker-stack.yml` Docker stack definition

Usage:

```bash
cd "Multi-Host Deployment KT/Linux-Docker-Swarm-NVR1Service"
ansible-playbook -i inventory.ini deploy-swarm.yml
```

Before running:

- update hostnames, SSH user, and private key path in `inventory.ini`
- update `rabbitmq_host` in `deploy-swarm.yml` if needed
- ensure the control machine runs the playbook from inside this folder so `../Demo/NVR1Service` resolves correctly
