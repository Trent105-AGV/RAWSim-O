#!/usr/bin/env python3
"""
dotnet-build-image.py

Usage:
    python3 dotnet-build-image.py

The script uses the existing Dockerfiles in each project directory and builds
the images from the repository root context as required by the Dockerfiles.
"""

import pathlib
import subprocess
import sys
from pathlib import Path

proj_root_path = pathlib.Path.cwd()


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


def build_dotnet_images():
    """Build Docker images for all .NET projects."""

    print("Building .NET Docker images")

    for project in dotnet_projects:
        print(f"- Preparing to build Docker image: {project.name}")

    count = 0
    for project in dotnet_projects:
        count += 1
        print(
            f"- [{count}/{len(dotnet_projects)}] Building Docker image: {project.name}")

        project_path = Path(proj_root_path) / project.path
        if not project_path.exists():
            print(f"  ERROR: Project path does not exist: {project_path}")
            sys.exit(1)

        dockerfile_path = project_path / "Dockerfile"
        if not dockerfile_path.exists():
            print(f"  ERROR: Dockerfile not found: {dockerfile_path}")
            sys.exit(1)

        # Use the new publish directory structure
        publish_dir = Path(proj_root_path) / "publish" / project.name
        if not publish_dir.exists():
            print(f"  ERROR: Publish directory does not exist: {publish_dir}")
            sys.exit(1)

        # Build the Docker image from the publish directory
        image_tag = f'{project.name}:latest'
        cmdlet = [
            'docker', 'build',
            '-f', str(dockerfile_path),
            '-t', image_tag,
            str(publish_dir)
        ]

        print(f"  Running: {' '.join(cmdlet)}")
        print(f"  Working directory: {proj_root_path}")

        result = subprocess.run(cmdlet, cwd=proj_root_path)
        if result.returncode != 0:
            print(f"  ERROR: Failed to build Docker image for {project.name}")
            sys.exit(1)

        print(f"  SUCCESS: Built image {image_tag}")

    print(f"All {len(dotnet_projects)} .NET Docker images built successfully!")


if __name__ == '__main__':
    build_dotnet_images()
