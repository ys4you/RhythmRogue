{
  description = "RhythmRogue - Unity 6 project plus Python beatmap tooling";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  };

  outputs = { self, nixpkgs }:
    let
      # Unity itself only ships for x86_64 Linux.
      system = "x86_64-linux";

      pkgs = nixpkgs.legacyPackages.${system};

      # Python environment for python/beatmap_generator.py and Tools/beat_analyzer.py
      pythonEnv = pkgs.python3.withPackages (ps: with ps; [
        librosa
        soundfile
        numpy
        scipy
      ]);

      # Run the beatmap scripts from the repo root:  nix run .#beat-analyzer -- song.ogg
      mkTool = name: script: pkgs.writeShellApplication {
        inherit name;
        runtimeInputs = [ pythonEnv pkgs.ffmpeg ];
        text = ''exec python ${script} "$@"'';
      };
    in {
      devShells.${system}.default = pkgs.mkShell {
        packages = [
          pythonEnv
          pkgs.ffmpeg     # needed by librosa for .ogg / .mp3
          pkgs.dotnet-sdk # optional: for IDE tooling / C# analysis outside Unity
        ];

        shellHook = ''
          echo "RhythmRogue dev shell"
          echo "  Unity Editor version: $(sed -n 's/m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt 2>/dev/null)"
          echo "  (Unity Hub is installed system-wide via dev.nix)"
          echo "  python python/beatmap_generator.py song.wav --bpm 135"
          echo "  python Tools/beat_analyzer.py song.ogg"
        '';
      };

      apps.${system} = {
        default = self.apps.${system}.beat-analyzer;

        beat-analyzer = {
          type = "app";
          program = "${mkTool "beat-analyzer" "Tools/beat_analyzer.py"}/bin/beat-analyzer";
        };

        beatmap-generator = {
          type = "app";
          program = "${mkTool "beatmap-generator" "python/beatmap_generator.py"}/bin/beatmap-generator";
        };
      };

      formatter.${system} = pkgs.nixpkgs-fmt;
    };
}
