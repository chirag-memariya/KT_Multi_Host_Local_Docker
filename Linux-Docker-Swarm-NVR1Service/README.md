# Linux Docker Swarm Multi-Node Hello PoC

This folder contains a Docker-based multi-node PoC that deploys a tiny hello service across swarm nodes.

Instead of requiring three real Linux machines, it starts three Ubuntu containers that behave like Linux swarm nodes:

- OpenSSH Server is enabled on every node so Ansible can connect over SSH
- Docker Engine runs inside every node container
- a lightweight image is pulled on every node
- the manager deploys a hello service as a three-replica swarm service

Files:

- `inventory.ini` inventory for the SSH-enabled demo swarm nodes
- `deploy-swarm.yml` Ansible playbook for swarm bootstrap, image build, and stack deployment
- `stack/docker-stack.yml` Docker stack definition for the lightweight hello service
- `lab/docker-compose.yml` local three-node lab environment
- `lab/node/Dockerfile` image used for the SSH-enabled swarm nodes

Requirements:

- Docker or Docker Desktop on the machine that will host the demo lab
- Ansible on the control machine
- `sshpass` available to Ansible if you use the default password-based inventory entries
- internet access from swarm nodes to pull a small base image

How it works:

1. `docker compose` builds and starts three Linux containers:
	- `swarm-manager`
	- `swarm-worker-1`
	- `swarm-worker-2`
2. Ansible connects to those containers through SSH on ports `2221`, `2222`, and `2223`
3. The manager initializes Docker Swarm
4. Workers join the swarm
5. A small image is pulled on each node with retries
6. The manager deploys the stack with one replica per node

Usage:

```bash
cd "Multi-Host Deployment KT/Linux-Docker-Swarm-NVR1Service"
docker compose -f lab/docker-compose.yml up -d --build
ansible-playbook -i inventory.ini deploy-swarm.yml
```

If your environment uses a corporate/intercepting TLS proxy, pass the proxy root CA
certificate so swarm nodes can pull images from registries:

```bash
ansible-playbook -i inventory.ini deploy-swarm.yml \
	-e swarm_custom_ca_cert_src="$PWD/lab/certs/corporate-root-ca.crt"
```

Demo checks:

- run `docker exec -it swarm-manager docker service ls`
- run `docker exec -it swarm-manager docker service ps nvr1_hello-service`
- run `docker exec -it swarm-manager docker service logs nvr1_hello-service`

Cleanup:

```bash
cd "Multi-Host Deployment KT/Linux-Docker-Swarm-NVR1Service"
docker compose -f lab/docker-compose.yml down -v
```

Before running:

- keep the default demo credentials only for local KT/demo use
- if you hit `x509: certificate signed by unknown authority`, provide
	`swarm_custom_ca_cert_src` pointing to your trusted corporate root CA certificate
