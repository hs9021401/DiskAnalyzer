# DiskAnalyzer ⚡

> **Analizador y visualizador rápido de espacio en disco para Windows**

DiskAnalyzer es una herramienta para Windows x64 desarrollada con WPF y .NET 10. Construye una jerarquía de archivos y carpetas, y ayuda a encontrar los elementos que ocupan más espacio mediante Tree View, File View, File Types y un Treemap interactivo.

## 🌐 Idiomas

[繁體中文](README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md)

## ✨ Funciones principales

- **Escaneo rápido**: usa `$MFT` para escanear volúmenes NTFS completos cuando los permisos lo permiten. Las carpetas, los volúmenes no NTFS, el acceso restringido y los fallos de lectura de `$MFT` se gestionan con el escaneo Win32 paralelo.
- **Varias vistas**: explora carpetas en el Tree View jerárquico, busca archivos grandes en File View, agrupa el uso por extensión en File Types o compara tamaños visualmente con el Treemap.
- **Operaciones del árbol**: selección múltiple con Ctrl/Shift, operaciones por lotes desde el menú contextual, expansión automática del primer nivel y apertura de archivos con doble clic.
- **Cálculo preciso**: deduplicación de hard links de NTFS y elementos virtuales Free Space y Allocated/System Space.
- **Integración con Windows**: abrir archivos, mostrarlos en el Explorador, copiar rutas y detalles, abrir CMD/PowerShell, mover a la Papelera, eliminar permanentemente y mostrar las Propiedades de Windows.
- **Exportación y traducciones**: exportación CSV estándar y cambio en tiempo de ejecución entre inglés, chino tradicional, chino simplificado, japonés, coreano, español y francés.

## 🖱️ Uso básico

1. Selecciona una unidad o carpeta y pulsa **Scan**.
2. Explora la jerarquía en Tree View, o cambia a File View y File Types para localizar archivos.
3. Usa Ctrl/Shift para seleccionar varios elementos y el menú contextual para ejecutar operaciones por lotes.
4. Haz doble clic en un archivo para abrirlo con la aplicación predeterminada de Windows; el menú contextual ofrece más acciones.
5. Usa las sugerencias, el zoom y la navegación jerárquica del Treemap para localizar rápidamente los elementos grandes.

Revisa las rutas seleccionadas antes de mover o eliminar archivos. Los elementos eliminados permanentemente no se pueden recuperar desde la Papelera.

## 📦 Descarga e instalación

### Versión Portable

Descarga `DiskAnalyzer_Portable_win-x64.zip` desde [GitHub Releases](https://github.com/hs9021401/DiskAnalyzer/releases), extráelo y ejecuta `DiskAnalyzer.exe`. La versión Portable no requiere instalación e incluye el runtime de .NET.

### Instalador de Inno Setup

Ejecuta el instalador de un Release y selecciona el idioma, el acceso directo del escritorio y la integración opcional con el menú contextual del Explorador de Windows.

## 💻 Requisitos

- Windows 10, Windows 11 o un Windows Server x64 compatible.
- Las versiones Portable e instalada no requieren una instalación adicional de .NET.
- El SDK de .NET 10 es necesario para compilar desde el código fuente.
- Los privilegios de administrador son opcionales, pero pueden mejorar el acceso y la cobertura del escaneo NTFS.

## 🔧 Compilar desde el código fuente

Ejecuta en Windows con PowerShell y el SDK de .NET 10:

```powershell
dotnet restore DiskAnalyzer.slnx
dotnet build src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj -c Debug
dotnet test tests/DiskAnalyzer.Tests/DiskAnalyzer.Tests.csproj --no-restore
```

Para crear una versión Portable self-contained de un solo archivo:

```powershell
dotnet publish src/DiskAnalyzer.UI/DiskAnalyzer.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## ⚠️ Notas y limitaciones

- DiskAnalyzer actualmente solo admite Windows x64; Linux y macOS no están soportados.
- Las carpetas protegidas, sin conexión o inaccesibles pueden omitirse.
- El escaneo, el traslado a la Papelera y las operaciones de eliminación afectan a los archivos seleccionados. Conserva copias de seguridad.
- La API pública aún no es estable; pueden producirse cambios entre versiones.

## 📄 Licencia y avisos de terceros

Este proyecto se distribuye bajo la [MIT License](LICENSE). Las versiones Portable e instalada incluyen `LICENSE.txt` y `THIRD-PARTY-NOTICES.txt`, con avisos y enlaces de licencia del runtime self-contained de .NET y sus dependencias relacionadas.

Copyright © 2026 Alex Lin.
