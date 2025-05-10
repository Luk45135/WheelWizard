{
  lib,
  buildDotnetModule,
  dotnetCorePackages,
  makeDesktopItem,
  copyDesktopItems,
}:
buildDotnetModule {
  pname = "WheelWizard";
  version = "2.2.1";

  src = ./.;
  projectFile = "WheelWizard/WheelWizard.csproj";

  dotnet-sdk = dotnetCorePackages.sdk_8_0;
  dotnet-runtime = dotnetCorePackages.runtime_8_0;

  nugetDeps = ./deps.json;
  selfContainedBuild = true;

  dotnetBuildFlags = [
    "-p:IncludeAllContentForSelfExtract=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true"
  ];

  executeables = ["WheelWizard"];
  useAppHost = true;

  nativeBuildInputs = [copyDesktopItems];

  postInstall = ''
    install -Dm444 Flatpak/io.github.TeamWheelWizard.WheelWizard.png $out/share/icons/hicolor/256x256/apps/car-wheel.png
  '';

  desktopItems = [ (makeDesktopItem {
    name = "wheelwizard";
    genericName = "Mario Kart Wii Mod Manager";
    comment = "Mario Kart Wii Mod Manager & Retro Rewind Auto Updater";
    desktopName = "WheelWizard";
    exec = "WheelWizard";
    icon = "car-wheel";
    type = "Application";
    terminal = false;
    categories = ["Game"];
  }) ];
}


