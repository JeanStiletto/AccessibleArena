# Accessible Arena

Mod de accesibilidad para Magic: The Gathering Arena que permite a jugadores ciegos y con discapacidad visual jugar usando un lector de pantalla. Navegación completa por teclado, anuncios del lector de pantalla para todos los estados del juego y localización en 12 idiomas.

**Estado:** Beta pública. La jugabilidad principal es funcional. Quedan algunos casos especiales y errores menores. Ver Problemas conocidos más abajo.

**Nota:** Actualmente solo teclado. No hay soporte para ratón o pantalla táctil. Solo probado en Windows 11 con NVDA. Otras versiones de Windows y lectores de pantalla (JAWS, Narrator, etc.) podrían funcionar pero no están probados.

## Características

- Navegación completa por teclado para todas las pantallas (inicio, tienda, maestría, constructor de mazos, duelos)
- Integración con lector de pantalla a través de la biblioteca Tolk
- Lectura de información de cartas con teclas de flecha (nombre, coste de maná, tipo, fuerza/resistencia, texto de reglas, texto de ambientación, rareza, artista)
- Soporte completo de duelos: navegación por zonas, combate, selección de objetivos, pila, navegadores (profecía, vigilar, mulligan)
- Anuncios de relaciones de acoplamiento y combate (encantado por, bloqueando, objetivo de)
- Tienda accesible con opciones de compra y soporte de diálogos de pago
- Soporte de partidas contra bots para practicar
- Menú de ajustes (F2) y menú de ayuda (F1) disponibles en todo momento
- 12 idiomas: inglés, alemán, francés, español, italiano, portugués (BR), japonés, coreano, ruso, polaco, chino simplificado, chino tradicional

## Requisitos

- Windows 10 o posterior
- Magic: The Gathering Arena (instalado mediante el instalador oficial o Epic Games Store)
- Un lector de pantalla (NVDA recomendado: https://www.nvaccess.org/download/)
- MelonLoader (el instalador lo gestiona automáticamente)

## Instalación

### Con el instalador (recomendado)

1. Descargue `AccessibleArenaInstaller.exe` de la última versión en GitHub: https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe
2. Cierre MTG Arena si está en ejecución
3. Ejecute el instalador. Detectará su instalación de MTGA, instalará MelonLoader si es necesario y desplegará el mod
4. Inicie MTG Arena. Debería escuchar "Accessible Arena v... iniciado" a través de su lector de pantalla

### Instalación manual

1. Instale MelonLoader en su carpeta de MTGA (https://github.com/LavaGang/MelonLoader)
2. Descargue `AccessibleArena.dll` de la última versión
3. Copie la DLL en: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
4. Asegúrese de que `Tolk.dll` y `nvdaControllerClient64.dll` estén en la carpeta raíz de MTGA
5. Inicie MTG Arena

## Inicio rápido

Si aún no tiene una cuenta de Wizards, puede crear una en https://myaccounts.wizards.com/ en lugar de usar la pantalla de registro del juego.

Después de la instalación, inicie MTG Arena. El mod anuncia la pantalla actual a través de su lector de pantalla.

- Pulse **F1** en cualquier momento para un menú de ayuda navegable con todos los atajos de teclado
- Pulse **F2** para el menú de ajustes (idioma, verbosidad, mensajes del tutorial)
- Pulse **F3** para escuchar el nombre de la pantalla actual
- Use **Flecha arriba/abajo** o **Tab/Mayús+Tab** para navegar por los menús
- Pulse **Intro** o **Espacio** para activar elementos
- Pulse **Retroceso** para volver atrás

## Atajos de teclado

### Menús

- Flecha arriba/abajo (o W/S): Navegar por elementos
- Tab/Mayús+Tab: Navegar por elementos (igual que Flecha arriba/abajo)
- Flecha izquierda/derecha (o A/D): Controles de carrusel y pasos
- Inicio/Fin: Saltar al primer/último elemento
- Re Pág/Av Pág: Página anterior/siguiente en la colección
- Intro/Espacio: Activar
- Retroceso: Volver

### Duelos - Zonas

- C: Tu mano
- G / Mayús+G: Tu cementerio / Cementerio del oponente
- X / Mayús+X: Tu exilio / Exilio del oponente
- S: Pila
- B / Mayús+B: Tus criaturas / Criaturas del oponente
- A / Mayús+A: Tus tierras / Tierras del oponente
- R / Mayús+R: Tus no-criaturas / No-criaturas del oponente

### Duelos - Dentro de zonas

- Izquierda/Derecha: Navegar entre cartas
- Inicio/Fin: Saltar a la primera/última carta
- Flecha arriba/abajo: Leer detalles de la carta cuando está enfocada
- I: Info extendida de la carta (descripciones de palabras clave, otras caras)
- Mayús+Arriba/Abajo: Cambiar filas del campo de batalla

### Duelos - Información

- T: Turno y fase actual
- L: Totales de vida
- V: Zona de info del jugador (Izquierda/Derecha para cambiar jugador, Arriba/Abajo para propiedades)
- D / Mayús+D: Cantidad de tu biblioteca / Biblioteca del oponente
- Mayús+C: Cantidad de cartas en mano del oponente

### Duelos - Acciones

- Espacio: Confirmar (pasar prioridad, confirmar atacantes/bloqueadores, siguiente fase)
- Retroceso: Cancelar / rechazar
- Tab: Recorrer objetivos o elementos resaltados
- Ctrl+Tab: Recorrer solo objetivos del oponente
- Intro: Seleccionar objetivo

### Duelos - Navegadores (Profecía, Vigilar, Mulligan)

- Tab: Navegar por todas las cartas
- C/D: Saltar a la zona superior/inferior
- Izquierda/Derecha: Navegar dentro de la zona
- Intro: Alternar colocación de carta
- Espacio: Confirmar selección
- Retroceso: Cancelar

### Global

- F1: Menú de ayuda
- F2: Menú de ajustes
- F3: Anunciar pantalla actual
- Ctrl+R: Repetir último anuncio
- Retroceso: Volver/cerrar/cancelar universal

## Reportar errores

Si encuentra un error, por favor abra un issue en GitHub: https://github.com/JeanStiletto/AccessibleArena/issues

Incluya la siguiente información:

- Qué estaba haciendo cuando ocurrió el error
- Qué esperaba que sucediera
- Qué sucedió realmente
- Su lector de pantalla y versión
- Adjunte el archivo de registro de MelonLoader: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## Problemas conocidos

- La tecla Espacio para pasar prioridad no siempre es fiable (el mod hace clic en el botón directamente como respaldo)
- Las cartas de la lista de mazo en el constructor solo muestran nombre y cantidad, no detalles completos
- La selección de tipo de cola PlayBlade (Clasificatoria, Juego Abierto, Brawl) no siempre establece el modo de juego correcto

Para la lista completa, ver docs/KNOWN_ISSUES.md.

## Solución de problemas

**Sin salida de voz después de iniciar el juego**
- Asegúrese de que su lector de pantalla esté ejecutándose antes de iniciar MTG Arena
- Verifique que `Tolk.dll` y `nvdaControllerClient64.dll` estén en la carpeta raíz de MTGA (el instalador los coloca automáticamente)
- Revise el registro de MelonLoader en `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log` en busca de errores

**El juego se bloquea al iniciar o el mod no se carga**
- Asegúrese de que MelonLoader esté instalado.
- Si el juego se actualizó recientemente, puede ser necesario reinstalar MelonLoader o el mod. Ejecute el instalador de nuevo.
- Verifique que `AccessibleArena.dll` esté en `C:\Program Files\Wizards of the Coast\MTGA\Mods\`

**El mod funcionaba pero dejó de funcionar tras una actualización del juego**
- Las actualizaciones de MTG Arena pueden sobrescribir archivos de MelonLoader. Ejecute el instalador de nuevo para reinstalar MelonLoader y el mod.
- Si el juego cambió significativamente su estructura interna, el mod puede necesitar una actualización. Busque nuevas versiones en GitHub.

**Los atajos de teclado no funcionan**
- Asegúrese de que la ventana del juego esté enfocada (haga clic en ella o use Alt+Tab)
- Pulse F1 para verificar si el mod está activo. Si escucha el menú de ayuda, el mod está funcionando.
- Algunos atajos solo funcionan en contextos específicos (los atajos de duelo solo durante un duelo)

**Idioma incorrecto**
- Pulse F2 para abrir el menú de ajustes, luego use Intro para recorrer los idiomas

## Compilar desde el código fuente

Requisitos: SDK .NET (cualquier versión que soporte net472 como objetivo)

```
git clone https://github.com/JeanStiletto/AccessibleArena.git
cd AccessibleArena
dotnet build src/AccessibleArena.csproj
```

La DLL compilada estará en `src/bin/Debug/net472/AccessibleArena.dll`.

Las referencias de ensamblado del juego se esperan en la carpeta `libs/`. Copie estas DLLs de su instalación de MTGA (`MTGA_Data/Managed/`):
- Assembly-CSharp.dll
- Core.dll
- UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
- Unity.TextMeshPro.dll, Unity.InputSystem.dll
- Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
- ZFBrowser.dll

Las DLLs de MelonLoader (`MelonLoader.dll`, `0Harmony.dll`) provienen de su instalación de MelonLoader.

## Licencia

Este proyecto está bajo la licencia GNU General Public License v3.0. Consulte el archivo LICENSE para más detalles.

## Enlaces

- GitHub: https://github.com/JeanStiletto/AccessibleArena
- Lector de pantalla NVDA (recomendado): https://www.nvaccess.org/download/
- MelonLoader: https://github.com/LavaGang/MelonLoader
- MTG Arena: https://magic.wizards.com/mtgarena
