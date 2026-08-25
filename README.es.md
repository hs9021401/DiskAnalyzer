# DiskAnalyzer ⚡

> **Analizador y visualizador rápido de espacio en disco para Windows**

DiskAnalyzer es una herramienta para Windows x64 creada con WPF y .NET 10. Construye la jerarquía de archivos usando `$MFT` de NTFS, USN Change Journal o un recorrido paralelo de carpetas mediante Win32, y muestra los resultados en Tree View, File View, File Types y un Treemap interactivo.

## 🌐 Idiomas

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ Funciones principales

- **Escaneo rápido**: lectura de MFT/Data Runs de NTFS cuando es posible, enumeración de USN Journal y alternativa Win32 multihilo para carpetas, volúmenes no NTFS o sesiones sin privilegios de administrador.
- **Varias vistas**: Tree View jerárquico, File View virtualizado para archivos grandes, resúmenes File Types por extensión y Treemap interactivo.
- **Selección de árbol**: selección múltiple con Ctrl/Shift, operaciones por lotes desde el menú contextual, expansión automática del primer nivel y apertura de archivos con doble clic.
- **Cálculo preciso**: eliminación de duplicados por hard links de NTFS y elementos virtuales Free Space y Allocated/System Space.
- **Integración con Windows Shell**: abrir archivos, mostrar en el Explorador, copiar rutas/detalles, abrir CMD o PowerShell, mover a la Papelera, eliminar permanentemente y mostrar Propiedades.
- **Exportación y traducciones**: exportación CSV estándar y cambio en tiempo de ejecución entre inglés, chino tradicional, chino simplificado, japonés, coreano, español y francés.

## 💻 Requisitos

- Windows 10, Windows 11 o un Windows Server x64 compatible.
- .NET 10 SDK para compilar desde el código fuente. La versión portable self-contained incluye el runtime.
- Los privilegios de administrador son opcionales, pero pueden mejorar el acceso a MFT y la cobertura del escaneo.

## 📦 Instalación y uso

Descarga `DiskAnalyzer_Portable_win-x64.zip` desde GitHub Releases, extráelo y ejecuta `DiskAnalyzer.exe`. La versión portable no requiere instalación. El instalador de Inno Setup permite elegir el idioma, crear un acceso directo y añadir la integración con el menú contextual del Explorador de Windows.

## 🔧 Compilar desde el código fuente

Ejecuta en Windows con PowerShell y .NET 10 SDK:

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

Para crear una versión portable self-contained de un solo archivo:

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## 🏗️ Estructura del proyecto

```text
src/DiskAnalyzer.Core/       Modelos, escáneres, MFT/USN, exportación y búsqueda
src/DiskAnalyzer.UI/         Aplicación WPF, ViewModels, controles, temas y recursos
tests/DiskAnalyzer.Tests/    Pruebas del núcleo y de componentes UI
DiskAnalyzer.iss             Script del instalador Inno Setup
```

## ⚠️ Notas y limitaciones

- El objetivo actual es Windows x64; Linux y macOS no están soportados.
- Las carpetas protegidas o inaccesibles pueden omitirse.
- El escaneo y las operaciones de eliminación afectan a los archivos seleccionados. Conserva copias de seguridad de los datos importantes.

## 🤝 Contribuciones y licencia

Lee [CONTRIBUTING.md](CONTRIBUTING.md) antes de abrir un Issue o Pull Request y elimina las rutas personales y los datos sensibles. El proyecto se distribuye bajo [MIT License](LICENSE).

Copyright © 2026 Alex Lin.
