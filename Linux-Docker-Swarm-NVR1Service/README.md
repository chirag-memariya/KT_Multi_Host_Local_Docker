# Linux Docker Swarm Multi-Node Demo for NVR1Service

This folder contains a Docker-based multi-node demo for the existing `Demo/NVR1Service` application.

Instead of requiring three real Linux machines, it starts three Ubuntu containers that behave like Linux swarm nodes:

- OpenSSH Server is enabled on every node so Ansible can connect over SSH
- Docker Engine runs inside every node container
- the same `Demo/NVR1Service` source is copied to every node and built locally
- the manager deploys the same app as a three-replica swarm service

Files:

- `inventory.ini` inventory for the SSH-enabled demo swarm nodes
- `deploy-swarm.yml` Ansible playbook for swarm bootstrap, image build, and stack deployment
- `stack/docker-stack.yml` Docker stack definition for the same NVR1Service app
- `lab/docker-compose.yml` local three-node lab environment
- `lab/node/Dockerfile` image used for the SSH-enabled swarm nodes

Requirements:

- Docker or Docker Desktop on the machine that will host the demo lab
- Ansible on the control machine
- `sshpass` available to Ansible if you use the default password-based inventory entries
- the existing app source present at `../Demo/NVR1Service`
- a valid `Dockerfile` inside `../Demo/NVR1Service`

How it works:

1. `docker compose` builds and starts three Linux containers:
	- `swarm-manager`
	- `swarm-worker-1`
	- `swarm-worker-2`
2. Ansible connects to those containers through SSH on ports `2221`, `2222`, and `2223`
3. The manager initializes Docker Swarm
4. Workers join the swarm
5. The same `NVR1Service` image is built on each node so no registry is required
6. The manager deploys the stack with one replica per node

Usage:

```bash
cd "Multi-Host Deployment KT/Linux-Docker-Swarm-NVR1Service"
docker compose -f lab/docker-compose.yml up -d --build
ansible-playbook -i inventory.ini deploy-swarm.yml
```

Demo checks:

- open `http://localhost:8080`
- run `docker exec -it swarm-manager docker service ls`
- run `docker exec -it swarm-manager docker service ps nvr1_nvr1service`

Cleanup:

```bash
cd "Multi-Host Deployment KT/Linux-Docker-Swarm-NVR1Service"
docker compose -f lab/docker-compose.yml down -v
```

Before running:

- update `rabbitmq_host` in `deploy-swarm.yml` if needed
- keep the default demo credentials only for local KT/demo use
- run the playbook from inside this folder so `../Demo/NVR1Service` resolves correctly
