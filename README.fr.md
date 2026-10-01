# DiskAnalyzer ⚡

> **Outil rapide d’analyse et de visualisation de l’espace disque pour Windows**

DiskAnalyzer est un outil Windows x64 développé avec WPF et .NET 10. Il construit une hiérarchie de fichiers et de dossiers, puis aide à repérer les éléments volumineux grâce à Tree View, File View, File Types et un Treemap interactif.

## 🌐 Langues

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ Fonctionnalités

- **Analyse rapide** : utilise `$MFT` pour analyser les volumes NTFS entiers lorsque les droits le permettent. Les dossiers, les volumes non NTFS, les accès restreints et les échecs de lecture de `$MFT` sont traités par l’analyse Win32 parallèle.
- **Vues multiples** : parcourez les dossiers dans le Tree View hiérarchique, trouvez les gros fichiers avec File View, regroupez l’espace par extension dans File Types ou comparez visuellement les tailles avec le Treemap.
- **Opérations dans l’arbre** : sélection multiple avec Ctrl/Shift, opérations groupées depuis le menu contextuel, ouverture automatique du premier niveau après l’analyse et ouverture des fichiers par double-clic.
- **Calcul précis** : déduplication des hard links NTFS et éléments virtuels Free Space et Allocated/System Space.
- **Intégration Windows** : ouvrir un fichier, l’afficher dans l’Explorateur, copier le chemin et les détails, ouvrir CMD/PowerShell, déplacer vers la Corbeille, supprimer définitivement et afficher les Propriétés Windows.
- **Export et localisation** : export CSV standard et changement à chaud entre anglais, chinois traditionnel, chinois simplifié, japonais, coréen, espagnol et français.

## 🖱️ Utilisation de base

1. Sélectionnez un lecteur ou un dossier, puis cliquez sur **Scan**.
2. Parcourez la hiérarchie dans Tree View, ou utilisez File View et File Types pour trouver les éléments recherchés.
3. Utilisez Ctrl/Shift pour sélectionner plusieurs éléments, puis le menu contextuel pour les opérations groupées.
4. Double-cliquez sur un fichier pour l’ouvrir avec l’application Windows par défaut ; le menu contextuel propose d’autres actions.
5. Utilisez les infobulles, le zoom et la navigation hiérarchique du Treemap pour localiser rapidement les gros éléments.

Vérifiez les chemins sélectionnés avant de déplacer ou de supprimer des fichiers. Les éléments supprimés définitivement ne peuvent pas être restaurés depuis la Corbeille.

## 📦 Téléchargement et installation

### Version Portable

Téléchargez `DiskAnalyzer_Portable_win-x64.zip` depuis [GitHub Releases](https://github.com/hs9021401/DiskAnalyzer/releases), décompressez-le et lancez `DiskAnalyzer.exe`. La version Portable ne nécessite aucune installation et inclut le runtime .NET.

### Installateur Inno Setup

Lancez l’installateur d’un Release et choisissez la langue, le raccourci du bureau et l’intégration facultative au menu contextuel de l’Explorateur Windows.

## 💻 Configuration requise

- Windows 10, Windows 11 ou Windows Server x64 compatible.
- Les versions Portable et installée ne nécessitent pas d’installation séparée de .NET.
- Le SDK .NET 10 est nécessaire pour compiler depuis les sources.
- Les droits administrateur sont facultatifs, mais peuvent améliorer l’accès et la couverture de l’analyse NTFS.

## 🔧 Compiler depuis les sources

Exécutez ces commandes sous Windows avec PowerShell et le SDK .NET 10 :

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

Pour créer une version Portable self-contained en un seul fichier :

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## ⚠️ Notes et limites

- DiskAnalyzer prend actuellement en charge Windows x64 uniquement ; Linux et macOS ne sont pas pris en charge.
- Les dossiers protégés, hors ligne ou inaccessibles peuvent être ignorés.
- L’analyse, le déplacement vers la Corbeille et les suppressions s’appliquent aux fichiers sélectionnés. Conservez une sauvegarde des données importantes.
- L’API publique n’est pas encore stable ; des changements peuvent survenir entre les versions.

## 📄 Licence et avis de tiers

Ce projet est distribué sous [MIT License](LICENSE). Les versions Portable et installée incluent `LICENSE.txt` et `THIRD-PARTY-NOTICES.txt`, qui contiennent les avis et liens de licence du runtime .NET self-contained et de ses dépendances.

Copyright © 2026 Alex Lin.
