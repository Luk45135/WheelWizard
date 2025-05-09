{
  lib,
  buildDotnetModule,
  dotnetCorePackages,
  ...
}:
buildDotnetModule {
  pname = "WheelWizard";
  version = "2.2.1";

  src = ./.;
  projectFile = "WheelWizard/WheelWizard.csproj";

  dotnet-sdk = dotnetCorePackages.sdk_8_0;
  dotnet-runtime = dotnetCorePackages.runtime_8_0;

  nugetDeps = ./deps.json;

  dotnetBuildFlags = [
    "-p:IncludeAllContentForSelfExtract=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true"
  ];
  executeables = ["WheelWizard"];
  useAppHost = true;
}


