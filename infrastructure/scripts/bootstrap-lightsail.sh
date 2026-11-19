#!/usr/bin/env bash
# ==============================================================================
# AgriDrone — AWS Lightsail Host Bootstrap Script
# Target OS: Ubuntu 24.04 LTS (x86_64) on AWS Lightsail
# Usage: sudo ./bootstrap-lightsail.sh
# ==============================================================================

set -euo pipefail

echo "===> [1/6] Updating system packages and installing prerequisites..."
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get upgrade -y
apt-get install -y ca-certificates curl gnupg jq git unzip fail2ban ufw

# Set Timezone
timedatectl set-timezone Asia/Bangkok
echo "Timezone configured: $(timedatectl show --property=Timezone --value)"

echo "===> [2/6] Configuring 16 GB Swapfile for memory safety..."
if [ ! -f /swapfile ]; then
    # Allocate 16GB swap
    fallocate -l 16G /swapfile || dd if=/dev/zero of=/swapfile bs=1M count=16384
    chmod 600 /swapfile
    mkswap /swapfile
    swapon /swapfile
    echo '/swapfile none swap sw 0 0' >> /etc/fstab
    
    # Kernel tuning for swap behavior
    sysctl vm.swappiness=15
    sysctl vm.vfs_cache_pressure=50
    cat <<EOF >> /etc/sysctl.d/99-agridrone-swap.conf
vm.swappiness=15
vm.vfs_cache_pressure=50
EOF
    echo "Swapfile created and activated successfully."
else
    echo "Swapfile already exists. Skipping."
fi
swapon --show

echo "===> [3/6] Installing official Docker Engine & Docker Compose plugin..."
if ! command -v docker &> /dev/null; then
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc

    echo \
      "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
      $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
      tee /etc/apt/sources.list.d/docker.list > /dev/null

    apt-get update
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    systemctl enable --now docker
    echo "Docker installed successfully: $(docker --version)"
else
    echo "Docker is already installed: $(docker --version)"
fi

echo "===> [4/6] Setting up deploy user and runtime directories..."
if ! id -u deploy &>/dev/null; then
    useradd -m -s /bin/bash -U deploy
    usermod -aG docker deploy
    echo "User 'deploy' created and added to docker group."
fi

INSTALL_DIR="/opt/agridrone"
install -d -m 0750 -o deploy -g deploy "${INSTALL_DIR}"
install -d -m 0700 -o deploy -g deploy "${INSTALL_DIR}/secrets"
install -d -m 0750 -o deploy -g deploy "${INSTALL_DIR}/backups"
install -d -m 0750 -o deploy -g deploy "${INSTALL_DIR}/releases"
install -d -m 0750 -o deploy -g deploy "${INSTALL_DIR}/scripts"
install -d -m 0750 -o deploy -g deploy "${INSTALL_DIR}/infrastructure/caddy"

echo "Directory structure created under ${INSTALL_DIR}"

echo "===> [5/6] Hardening UFW firewall on host..."
# Notice: Lightsail also has an external cloud firewall
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp comment 'SSH'
ufw allow 80/tcp comment 'HTTP (Caddy)'
ufw allow 443/tcp comment 'HTTPS (Caddy)'
ufw --force enable

echo "===> [6/6] Bootstrap completed successfully!"
echo "Next steps:"
echo "1. Place production secrets into /opt/agridrone/secrets/.env.production (chmod 0600)"
echo "2. Copy compose.production.yaml, infrastructure/caddy/Caddyfile, and scripts into /opt/agridrone/"
echo "3. Run /opt/agridrone/scripts/deploy.sh <GIT_SHA>"
