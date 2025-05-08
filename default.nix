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
  projectFile = "WheelWizard.sln";

  dotnet-sdk = dotnetCorePackages.sdk_8_0;
  dotnet-runtime = dotnetCorePackages.runtime_8_0;

  nugetDeps = ./deps.json;

  executeables = ["WheelWizard"];
}


