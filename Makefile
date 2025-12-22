# PROJECT_NAME = RAWSim-O
# DIST_DIR = dist
# PUBLISH_DIR = $(DIST_DIR)/publish
# # RUNTIME = win-x86

# .PHONY: build publish run clean

# build:
# 	dotnet build -c XPlatRelease

# publish:
# 	dotnet publish -c XPlatRelease -o $(PUBLISH_DIR)



# # publish:
# # 	dotnet publish -c Release -r $(RUNTIME) --self-contained -p:EnableWindowsTargeting=true -o $(PUBLISH_DIR)

# # publish-cli:
# # 	dotnet publish RAWSimO.CLI/RAWSimO.CLI.csproj -c Release --self-contained -p:EnableWindowsTargeting=true /p:PublishSingleFile=true -o $(PUBLISH_DIR)

# # publish-visual:
# # 	dotnet publish RAWSimO.Visualization/RAWSimO.Visualization.csproj -c Release -r $(RUNTIME) --self-contained  /p:PublishSingleFile=true -o $(PUBLISH_DIR)

# # run-cli:
# # 	wine $(PUBLISH_DIR)/RAWSimO.CLI.exe

# # run-visual:
# # 	wine $(PUBLISH_DIR)/RAWSimO.Visualization.exe

# clean:
# 	dotnet clean -p:EnableWindowsTargeting=true
# 	rm -rf $(DIST_DIR)

all: build push

.PHONY: clean
clean:
	@echo "Cleaning project..."
	@dotnet clean 

.PHONY: build
build:
	@echo "Publishing projects to separate directories..."
	@dotnet publish RAWSimO.WebServer/RAWSimO.WebServer.csproj -f net10.0 -c Release -o publish/rawsimo
	@echo "Building Docker images..."
	@python3 tool/build-image.py

.PHONY: push
push:
	@echo "Pushing .NET Docker images..."
	@python3 tool/push-image.py

.PHONY: push-main
push-main:
	@echo "Pushing .NET Docker images to production..."
	@python3 tool/push-image.py --main

.PHONY: build-and-push
build-and-push: build push

.PHONY: clean-build-and-push
clean-build-and-push: clean build push

.PHONY: build-and-push-main
build-and-push-main: clean build push-main
