PROJECT_NAME = RAWSim-O
DIST_DIR = dist
PUBLISH_DIR = $(DIST_DIR)/publish
# RUNTIME = win-x86

.PHONY: build publish run clean

build:
	dotnet build -c XPlatRelease -p:EnableWindowsTargeting=true

publish:
	dotnet publish -c XPlatRelease -p:EnableWindowsTargeting=true

# publish:
# 	dotnet publish -c Release -r $(RUNTIME) --self-contained -p:EnableWindowsTargeting=true -o $(PUBLISH_DIR)

# publish-cli:
# 	dotnet publish RAWSimO.CLI/RAWSimO.CLI.csproj -c Release --self-contained -p:EnableWindowsTargeting=true /p:PublishSingleFile=true -o $(PUBLISH_DIR)

# publish-visual:
# 	dotnet publish RAWSimO.Visualization/RAWSimO.Visualization.csproj -c Release -r $(RUNTIME) --self-contained -p:EnableWindowsTargeting=true /p:PublishSingleFile=true -o $(PUBLISH_DIR)

# run-cli:
# 	wine $(PUBLISH_DIR)/RAWSimO.CLI.exe

run-visual:
	wine $(PUBLISH_DIR)/RAWSimO.Visualization.exe

clean:
	dotnet clean
	rm -rf $(DIST_DIR)