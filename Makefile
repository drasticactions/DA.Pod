ROOT=$(PWD)
BUILD_TYPE=Release
CLIAPP=$(ROOT)/src/DA.Pod/DA.Pod.csproj

macos_artifact:
	mkdir -p artifacts/macos
	dotnet publish $(CLIAPP) -c Release -o artifacts/macos

linux_x64_artifact:
	mkdir -p artifacts/linux-x64
	dotnet publish $(CLIAPP) -c Release -o artifacts/linux-x64 -r linux-x64