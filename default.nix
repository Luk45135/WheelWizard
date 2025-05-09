{
  lib,
  buildDotnetModule,
  dotnetCorePackages,
  makeDesktopItem,
  icoutils,
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

  postInstall =
    ''
      icotool --icon -x car-wheel.ico
      for i in 16 32 48 256; do
        size=''${i}x''${i}
        install -Dm444 *_''${size}x32.png $out/share/icons/hicolor/$size/apps/car-wheel.png
      done
    '';

  desktopItems = makeDesktopItem {
    name = "wheelwizard";
    genericname = "Mario Kart Wii Mod Manager";
    comment = "Mario Kart Wii Mod Manager & Retro Rewind Auto Updater";
    desktopName = "WheelWizard";
    exec = "WheelWizard";
    icon = "car-wheel";
    type = "Application";
    terminal = false;
    categories = [
      "Game"
    ];
  };
}


