# Minecraft Modpack Installer

A **simple, cross-platform tool** created for people who **don't want to struggle with installing mods** for Minecraft!  
This project is designed to make the process of updating or installing large modpacks as painless as possible—no more manual downloads, file copying, or Minecraft crashes due to missing files.

---

## ✨ Features

- **Easy to use**: Just run and follow the instructions—no complex setup!
- **Cross-platform**: Works on **Windows, macOS, and Linux**.
- **Safe by default**: Your current `mods` folder is moved to a timestamped `mods-backup-YYYYMMDD-HHMMSS` folder before new mods are installed. Nothing is ever deleted without your confirmation.
- **Many sources**: install from a **URL**, a **local `.zip` / `.mrpack` file** (Browse button, drag and drop, or `--file`), or a **Modrinth modpack** (project slug or link).
- **Understands `.mrpack`**: downloads the files listed in `modrinth.index.json`, verifies their SHA-1/SHA-512 hashes and applies `overrides/` and `client-overrides/`.
- **Auto-detects ZIP layouts**: full Minecraft folder layouts and legacy "flat jar" ZIPs both work.
- **Careful with your settings**: choose whether existing config files are overwritten, kept, or backed up first. `options.txt` and `servers.dat` are never overwritten by default.

---

## 🚀 Usage

### 1. Using the Installer

1. **Download the GUI version** of the installer from the [Releases](../../releases) tab.  
2. Launch the application by double-clicking it.  
3. Paste a **modpack URL**, a **Modrinth link or slug**, or pick a local file with **Browse...** (you can also drag a `.zip` / `.mrpack` onto the window).  
4. Choose whether to back up your mods folder and what to do with existing config files.  

---

### For Server Owners

Want to make installing modpacks even easier for your players or server community?

- Host your modpack somewhere example: a github.io website.

---

## 🛠 Building From Source

1. Install [.NET 9 SDK](https://dotnet.microsoft.com/download)
2. Clone this repo:
   ```
   git clone https://github.com/MrGidor/Minecraft_Modpack_Installer.git
   ```
3. Build:
   ```
   dotnet publish Gui/Gui.csproj -c Release -r win-x64 /p:PublishSingleFile=true /p:PublishTrimmed=false
   ```
   Replace `win-x64` with `linux-x64` or `osx-x64` as needed.
5. Your standalone executable will be in the `\bin\Release\net9.0\win-x64\publish` folder.

---

### Running the tests

```
dotnet test
```

---

## 📦 Supported Modpack Formats

The installer looks at the **contents** of the archive, not its file name:

| Format | Detected by | What happens |
| --- | --- | --- |
| **`.mrpack`** (Modrinth) | `modrinth.index.json` at the root | Files are downloaded and hash-checked, then `overrides/` and `client-overrides/` are extracted into the Minecraft folder. Files marked `env.client = unsupported` are skipped. `server-overrides/` is ignored. |
| **Full layout ZIP** | top-level `mods/`, `config/`, `resourcepacks/`, `shaderpacks/`, `saves/`, `scripts/`, `defaultconfigs/`, `kubejs/`, `datapacks/`, `schematics/` | Extracted into the Minecraft folder. Other root files are ignored. |
| **Flat jar ZIP** (legacy) | `.jar` files and none of the folders above | Everything is extracted into `mods/`. |

A single wrapper folder inside the ZIP (for example `MyPack/mods/...`) is removed automatically. The installer does **not** install Minecraft or a mod loader; it tells you which versions an `.mrpack` requires.

### Safety rules

- Entries with `..` or absolute paths are rejected before anything is written.
- `.mrpack` downloads are only accepted from `https` URLs on `cdn.modrinth.com`, `github.com`, `raw.githubusercontent.com` and `gitlab.com`, and every file must match its SHA-1/SHA-512 hash. Files are downloaded to a staging folder first, so a failed download leaves your mods untouched.
- Launcher files (`launcher_*`) and the `versions`, `libraries`, `assets` and `runtime` folders are never written.
- Your previous `mods` folder is only deleted if you turn the backup off **and** confirm.

### Config policy

| Policy | Existing file |
| --- | --- |
| Backup, then overwrite (default) | copied to `modpack_installer/config-backup-<timestamp>/`, then replaced |
| Overwrite | replaced |
| Keep existing | left as is |

`options.txt` and `servers.dat` are never overwritten unless you pass `--overwrite-protected`.

---

## 🤝 Contributing

PRs are welcome! If you have an idea for a new feature, open an issue or submit a pull request.

---

## 📄 License

[MIT](LICENSE)

---

**Enjoy your modded Minecraft, hassle-free!**
