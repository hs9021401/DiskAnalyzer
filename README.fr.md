# DiskAnalyzer ⚡

> **Outil rapide d’analyse et de visualisation de l’espace disque pour Windows**

DiskAnalyzer est un outil Windows x64 développé avec WPF et .NET 10. Il construit la hiérarchie des fichiers à partir de `$MFT` NTFS, de l’USN Change Journal ou d’un parcours parallèle des dossiers Win32, puis affiche les résultats dans Tree View, File View, File Types et un Treemap interactif.

## 🌐 Langues

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ Fonctionnalités

- **Analyse rapide** : lecture de MFT/Data Runs NTFS lorsque c’est possible, énumération de l’USN Journal et solution de repli Win32 multithread pour les dossiers, volumes non NTFS ou sessions sans droits administrateur.
- **Vues multiples** : Tree View hiérarchique, File View virtualisé pour les gros fichiers, statistiques File Types par extension et Treemap interactif.
- **Sélection dans l’arbre** : sélection multiple avec Ctrl/Shift, opérations groupées depuis le menu contextuel, expansion automatique du premier niveau et ouverture des fichiers par double-clic.
- **Calcul précis** : déduplication des hard links NTFS et éléments virtuels Free Space et Allocated/System Space.
- **Intégration Windows Shell** : ouvrir un fichier, l’afficher dans l’Explorateur, copier le chemin ou les détails, ouvrir CMD ou PowerShell, déplacer vers la Corbeille, supprimer définitivement et afficher les Propriétés Windows.
- **Export et localisation** : export CSV standard et changement à chaud entre anglais, chinois traditionnel, chinois simplifié, japonais, coréen, espagnol et français.

## 💻 Configuration requise

- Windows 10, Windows 11 ou Windows Server x64 compatible.
- .NET 10 SDK pour compiler depuis les sources. La version portable self-contained contient le runtime.
- Les droits administrateur sont facultatifs, mais peuvent améliorer l’accès à MFT et la couverture de l’analyse.

## 📦 Installation et utilisation

Téléchargez `DiskAnalyzer_Portable_win-x64.zip` depuis GitHub Releases, décompressez-le et lancez `DiskAnalyzer.exe`. La version portable ne nécessite pas d’installation. L’installateur Inno Setup permet de choisir la langue, de créer un raccourci et d’ajouter l’intégration au menu contextuel de l’Explorateur Windows.

## 🔧 Compiler depuis les sources

Exécutez ces commandes sous Windows avec PowerShell et le .NET 10 SDK :

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

Pour créer une version portable self-contained en un seul fichier :

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## 🏗️ Structure du projet

```text
src/DiskAnalyzer.Core/       Modèles, analyseurs, MFT/USN, export et recherche
src/DiskAnalyzer.UI/         Application WPF, ViewModels, contrôles, thèmes et ressources
tests/DiskAnalyzer.Tests/    Tests du cœur et des composants UI
DiskAnalyzer.iss             Script de l’installateur Inno Setup
```

## ⚠️ Notes et limites

- La cible actuelle est Windows x64 ; Linux et macOS ne sont pas pris en charge.
- Les dossiers protégés ou inaccessibles peuvent être ignorés.
- L’analyse, la suppression et la suppression définitive agissent sur les fichiers sélectionnés. Conservez une sauvegarde des données importantes.

## 🤝 Contributions et licence

Lisez [CONTRIBUTING.md](CONTRIBUTING.md) avant d’ouvrir une Issue ou une Pull Request et supprimez les chemins personnels et les données sensibles. Le projet est distribué sous [MIT License](LICENSE).

Copyright © 2026 Alex Lin.
