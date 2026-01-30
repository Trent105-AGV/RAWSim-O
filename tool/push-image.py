#!/usr/bin/env python3
"""
dotnet-push-image.py

Usage:
    python3 dotnet-push-image.py

Note: This script assumes the Docker images have already been built.
Use dotnet-build-image.py to build the images first.

Configuration (at the top of the script):
- REGISTRY_HOST: Registry hostname (default: 127.0.0.1)
- REGISTRY_PORT: Registry port (default: 5000)  
- USE_PORT_FORWARD: Whether to use kubectl port-forward (default: False)
- KUBE_NAMESPACE: Kubernetes namespace for port-forward (default: default)
- KUBE_SERVICE: Kubernetes service for port-forward (default: registry)

The script can optionally use kubectl port-forward to access a remote registry.
"""

import socket
import subprocess
import sys
import time
from typing import Optional
import argparse

# ===== Configurable constants =====
# Registry endpoint
REGISTRY_HOST = '127.0.0.1'
REGISTRY_HOST_PROD = '127.0.0.1'
REGISTRY_PORT = 5000

# Port-forward options (optional)
USE_PORT_FORWARD = False
KUBE_NAMESPACE = 'default'
KUBE_SERVICE = 'registry'
# If None, remote service port == local REGISTRY_PORT
REMOTE_PORT = None
PORT_FORWARD_TIMEOUT_SEC = 20.0


class DotNetProject:
    def __init__(self, name, path, project_file):
        self.name = name
        self.path = path
        self.project_file = project_file


# .NET projects configuration
dotnet_projects = [
    DotNetProject(name='rawsimo', path='RAWSimO.WebServer',
                  project_file='RAWSimO.WebServer.csproj'),
]


def _wait_for_port(host: str, port: int, timeout_sec: float = 20.0) -> bool:
    """Wait for a port to become available."""
    deadline = time.time() + timeout_sec
    while time.time() < deadline:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.settimeout(0.5)
            try:
                s.connect((host, port))
                return True
            except Exception:
                pass
        time.sleep(0.5)
    return False


def start_port_forward(namespace: str = KUBE_NAMESPACE, service: str = KUBE_SERVICE, local_port: int = REGISTRY_PORT, remote_port: Optional[int] = None,
                       wait_host: str = REGISTRY_HOST, wait_port: int = REGISTRY_PORT, timeout_sec: float = PORT_FORWARD_TIMEOUT_SEC):
    """Start kubectl port-forward and wait until the port is reachable.

    Returns the Popen process if successful; raises RuntimeError on failure.
    """
    rp = remote_port or local_port
    print(
        f"- Starting port-forward: kubectl port-forward svc/{service} {local_port}:{rp} -n {namespace}")
    proc = subprocess.Popen([
        'kubectl', 'port-forward', f'svc/{service}', f'{local_port}:{rp}', '-n', namespace
    ])
    if not _wait_for_port(wait_host, wait_port, timeout_sec=timeout_sec) or proc.poll() is not None:
        try:
            if proc.poll() is None:
                proc.terminate()
                proc.wait(timeout=5)
        except Exception:
            try:
                proc.kill()
            except Exception:
                pass
        raise RuntimeError(
            f"Failed to establish port-forward to {wait_host}:{wait_port} within {timeout_sec}s")
    return proc


def stop_port_forward(proc: Optional[subprocess.Popen]):
    """Stop kubectl port-forward process."""
    if proc is not None and proc.poll() is None:
        try:
            proc.terminate()
            proc.wait(timeout=5)
        except Exception:
            try:
                proc.kill()
            except Exception:
                pass


def push_dotnet_images(use_port_forward: bool = USE_PORT_FORWARD, namespace: str = KUBE_NAMESPACE, service: str = KUBE_SERVICE, remote_port: Optional[int] = REMOTE_PORT, timeout_sec: float = PORT_FORWARD_TIMEOUT_SEC,
                       registry_host: str = REGISTRY_HOST, registry_port: int = REGISTRY_PORT):
    """Push Docker images for all .NET projects (assumes images are already built)."""

    print(f"Pushing .NET Docker images to {registry_host}:{registry_port}")

    count = 0
    pf_proc = None
    try:
        if use_port_forward:
            pf_proc = start_port_forward(namespace=namespace, service=service, local_port=registry_port,
                                         remote_port=remote_port, wait_host=registry_host, wait_port=registry_port, timeout_sec=timeout_sec)

        for project in dotnet_projects:
            count += 1
            print(
                f"- [{count}/{len(dotnet_projects)}] Pushing Docker image: {project.name}")

            # Tag with latest and then tag with registry
            image_tag = f'{project.name}:latest'
            registry_tag = f'{registry_host}:{registry_port}/{project.name}:latest'

            # Tag the local image for the registry
            tag_cmdlet = ['docker', 'tag', image_tag, registry_tag]
            result = subprocess.run(tag_cmdlet)
            if result.returncode != 0:
                print(
                    f"  ERROR: Failed to tag Docker image for {project.name}")
                sys.exit(1)

            # Push to registry
            push_cmdlet = ['docker', 'push', registry_tag]
            result = subprocess.run(push_cmdlet)
            if result.returncode != 0:
                print(
                    f"  ERROR: Failed to push Docker image for {project.name}")
                sys.exit(1)

            print(f"  SUCCESS: Pushed image {registry_tag}")
    finally:
        stop_port_forward(pf_proc)

    print(f"All {len(dotnet_projects)} .NET Docker images pushed successfully!")


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Push .NET Docker images')
    parser.add_argument('--main', action='store_true',
                        help='Use production registry host instead of development host')

    args = parser.parse_args()

    # 根据是否使用main标签选择不同的registry host
    registry_host = REGISTRY_HOST_PROD if args.main else REGISTRY_HOST

    print(f"Pushing .NET Docker images to {registry_host}:{REGISTRY_PORT}")
    if args.main:
        print("📍 Using production registry host")

    # Push only (assumes images are already built)
    push_dotnet_images(
        use_port_forward=USE_PORT_FORWARD,
        namespace=KUBE_NAMESPACE,
        service=KUBE_SERVICE,
        remote_port=REMOTE_PORT,
        timeout_sec=PORT_FORWARD_TIMEOUT_SEC,
        registry_host=registry_host,
        registry_port=REGISTRY_PORT,
    )
