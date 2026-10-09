# Echo Nano FLAC

Aplicación de escritorio para Windows que prepara música FLAC y Opus para
reproductores Echo Nano. Proyecto comunitario independiente, desarrollado con
asistencia de ChatGPT/Codex de OpenAI para programación, diseño y pruebas.
No está afiliado, certificado ni patrocinado por OpenAI, FiiO o SNOWSKY.

## Funciones

- FLAC → FLAC sin pérdida, conservando frecuencia, bits y canales.
- Opus mono/estéreo estándar → FLAC de 48 kHz/24 bits. No recupera información
  perdida en Opus; verifica contra su decodificación PCM de 24 bits.
- Bloques FLAC de 4096 muestras, salvo el último bloque parcial.
- Conserva artista, álbum, título, pista y otras etiquetas básicas; elimina carátulas.
- Compara audio decodificado, cabeceras, etiquetas y bloques antes de publicar copias.
- Los originales y las copias existentes no se sobrescriben.
- Menús Archivo, Edición, Ver y Ayuda. Tema del sistema/claro/oscuro y fondos nativos.
- Instalación por usuario y preferencias en `%LOCALAPPDATA%\EchoNanoFLAC`.

La aplicación prepara archivos ya extraídos: no extrae directamente discos CD.
La compatibilidad final debe probarse en el reproductor y su firmware.

## Compilar

Requisitos: Windows x64, .NET Framework 4.8 y FFmpeg para Windows x64.
Para editar en Visual Studio, instala la carga de trabajo de escritorio .NET
y abre `Source/EchoNanoFLAC.csproj`. La interfaz está escrita en C#; no hay
archivos del diseñador visual. Coloca `ffmpeg.exe` en la raíz del proyecto.

También puedes compilar con el compilador C# de .NET Framework:

```powershell
.\build.ps1 -FfmpegPath 'C:\ruta\ffmpeg.exe'
```

El resultado queda en `dist\app`. El script no descarga ni instala dependencias.
Para generar el instalador se necesita Inno Setup 7:

```powershell
.\build.ps1 -FfmpegPath 'C:\ruta\ffmpeg.exe' -IsccPath 'C:\ruta\ISCC.exe'
```

## Antes de publicar

Este paquete está preparado para publicar **el código fuente propio**. No contiene
binarios, canciones, datos personales ni archivos de preferencias.

El código propio se ofrece bajo MIT; consulta `LICENSE`. FFmpeg tiene una licencia
independiente. La versión utilizada durante el desarrollo fue FFmpeg 7.1 essentials
de Gyan, con GPL v3 o posterior. No está incluida en este repositorio.

Si publicas un instalador que incluya FFmpeg, proporciona el código fuente
correspondiente a **esa compilación exacta**, bibliotecas y materiales de compilación
exigidos por sus licencias. Los enlaces generales de procedencia no sustituyen ese
requisito. Revisa `THIRD-PARTY-NOTICES.txt`, la licencia completa incluida y la
[información oficial de FFmpeg](https://ffmpeg.org/legal.html).
Puedes publicar primero el código y dejar la descarga de FFmpeg a cada usuario.

## Contribuir

Se aceptan propuestas y mejoras mediante issues y pull requests. Consulta
`CONTRIBUTING.md`. Conserva los avisos de licencia y documenta cambios en el audio.

## Créditos

Proyecto creado por su promotor y colaboradores con ayuda de ChatGPT/Codex de
OpenAI. Esta atribución es informativa y no añade restricciones a MIT ni atribuye
a OpenAI derechos sobre las contribuciones del proyecto. Motor de audio: FFmpeg,
desarrollado por sus propios autores. Icono propio: `Source/logo.svg`.
