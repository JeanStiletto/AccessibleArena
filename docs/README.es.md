<h1>Accessible Arena</h1>

<h2>Qué es este mod</h2>

Este mod te permite jugar a Arena, la representación digital más popular y amigable para principiantes del JCC Magic: The Gathering. Añade soporte completo para lector de pantalla y navegación por teclado a casi todos los aspectos del juego.

El mod admite todos los idiomas a los que el juego está traducido. Además, algunos idiomas que el propio juego no admite están parcialmente cubiertos: en esos, se traducen los anuncios propios del mod como textos de ayuda y pistas de interfaz, mientras que los datos de cartas y del juego permanecen en el idioma por defecto del juego.

<h2>Qué es Magic: The Gathering</h2>

Magic es un juego de cartas coleccionables registrado por Wizards of the Coast que te permite jugar como un mago contra otros magos, lanzando hechizos representados por las cartas. Existen 5 colores en Magic que representan distintas identidades de juego y ambientación. Si conoces Hearthstone o Yu-Gi-Oh, reconocerás muchos conceptos porque Magic es el ancestro de todos esos juegos.
Si quieres aprender más sobre Magic en general, la web del juego y muchos creadores de contenido te ayudarán.

<h2>Requisitos</h2>

- Windows 10 o posterior
- Magic: The Gathering Arena (instalado mediante el instalador oficial de Wizards o Steam)
- Un lector de pantalla (solo NVDA y JAWS están probados)
- MelonLoader (el instalador se encarga de esto automáticamente)

<h2>Instalación</h2>

<h3>Usando el instalador (recomendado)</h3>

1. [Descarga AccessibleArenaInstaller.exe](https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe) de la última release en GitHub
2. Cierra MTG Arena si está en ejecución
3. Ejecuta el instalador. Detectará tu instalación de MTGA, instalará MelonLoader si es necesario y desplegará el mod
4. Inicia MTG Arena. Deberías oír "Accessible Arena v... launched" a través de tu lector de pantalla

<h3>Instalación manual</h3>

1. Instala [MelonLoader](https://github.com/LavaGang/MelonLoader) en tu carpeta de MTGA
2. Descarga `AccessibleArena.dll` de la última release
3. Copia la DLL en tu carpeta Mods de MTGA:
   - Instalación WotC: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
   - Instalación Steam: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\Mods\`
4. Asegúrate de que `Tolk.dll` y `nvdaControllerClient64.dll` estén en la carpeta raíz de MTGA
5. Inicia MTG Arena

<h2>Desinstalación</h2>

Ejecuta el instalador de nuevo. Si el mod ya está instalado, ofrecerá una opción de desinstalación. Opcionalmente puedes también eliminar MelonLoader. Para desinstalar manualmente, elimina `AccessibleArena.dll` de la carpeta `Mods\` y quita `Tolk.dll` y `nvdaControllerClient64.dll` de la carpeta raíz de MTGA.

<h2>Si vienes de Hearthstone</h2>

Si has jugado Hearthstone Access, reconocerás muchas cosas por buenos motivos, porque no solo los principios de juego son parecidos, sino porque seguí muchos principios de diseño. Aun así, algunas cosas son diferentes.

Primero tienes más zonas por las que navegar, porque Magic conoce el cementerio, el exilio y algunas zonas extra. Tu campo de batalla no tiene un tamaño limitado y tiene filas de ordenación adicionales para hacer más manejable la cantidad de cosas que pueden aparecer.

Tu maná no sube automáticamente, sino que viene de cartas de tierra de distintos colores que tienes que jugar activamente. En consecuencia, los costes de maná tienen partes incoloras y de color que sumadas dan el coste total que debes cumplir para una carta.

No puedes atacar criaturas directamente, solo se puede atacar a oponentes y a algunas cartas muy específicas (planeswalkers y batallas). Como defensor tienes que decidir si quieres bloquear un ataque para que las criaturas luchen. Si no bloqueas, el daño impactará a tu avatar pero tus criaturas pueden vivir intactas. Además el daño no se acumula en las criaturas, sino que se cura al final de cada turno, tanto al final del tuyo como al del oponente. Para interactuar con criaturas del oponente que se niegan a luchar, debes jugar cartas específicas o presionar los puntos de vida del oponente tan fuerte que no tenga más opción que sacrificar criaturas valiosas para sobrevivir.

El juego tiene fases de combate muy bien diferenciadas que permiten acciones concretas como robar, lanzar hechizos o luchar. Por eso Magic te permite y te anima a hacer cosas en los turnos del oponente. No más quedarse sentado esperando mientras pasan cosas. Juega un mazo interactivo y destruye los planes enemigos al vuelo.

<h2>Primeros pasos</h2>

El juego primero te pide que introduzcas algunos datos sobre ti y que registres un personaje. Esto debería funcionar a través de los medios internos del juego, pero si no, puedes usar alternativamente la web del juego, que es totalmente accesible.

El juego comienza con un tutorial donde aprendes los fundamentos de Magic: The Gathering. El mod añade pistas de tutorial propias para usuarios de lector de pantalla junto al tutorial estándar. Al terminar el tutorial, te recompensan con 5 mazos iniciales, uno por cada color.

A partir de ahí, tienes varias opciones para desbloquear más cartas y aprender el juego:

- **Desafíos de color:** Juega el desafío de color para cada uno de los cinco colores de Magic. Cada desafío te hace enfrentarte a 4 oponentes NPC, seguido de una partida contra un jugador real al final.
- **Eventos de mazos iniciales:** Juega uno de los 10 mazos de dos colores contra humanos reales que tienen las mismas opciones de mazo disponibles.
- **Jump In:** Elige dos paquetes de 20 cartas de distintos colores y temas, combínalos en un mazo y juega contra humanos reales con opciones similares. Recibes fichas gratuitas para este evento y te quedas las cartas elegidas.
- **Spark Ladder:** En algún momento se desbloquea la Spark Ladder, donde juegas tus primeras partidas clasificatorias contra oponentes reales.

Revisa tu correo en el menú social, ya que contiene muchas recompensas y sobres de cartas.

El juego desbloquea modos gradualmente según qué y cuánto juegues. Te da pistas y misiones en el menú de progreso y objetivos, y destaca los modos relevantes bajo el menú jugar. Una vez completes suficiente contenido de nuevo jugador, todos los distintos modos y eventos estarán totalmente disponibles.

En el Códex del Multiverso puedes aprender sobre modos de juego y mecánicas. Se amplía con el progreso creciente en la experiencia de nuevo jugador.

En ajustes de cuenta puedes saltarte todas las experiencias de tutorial y forzar el desbloqueo de todo para tener libertad completa desde el principio. Sin embargo, jugar los eventos de nuevo jugador te da muchas cartas y es recomendable para nuevos jugadores. Solo desbloquea todo pronto si ya sabes lo que estás haciendo. Si no, el contenido para principiantes ofrece mucha diversión y aprendizaje mientras te guía bien.

<h2>Atajos de teclado</h2>

La navegación sigue convenciones estándar en todas partes: flechas para moverse, Inicio/Fin para saltar al primero/último, Intro para seleccionar, Espacio para confirmar, Retroceso para volver o cancelar. Tab/Shift+Tab también funciona para navegar. Re Pág/Av Pág cambia de página.

<h3>Global</h3>

- F1: Menú de ayuda (lista todos los atajos para la pantalla actual)
- Ctrl+F1: Anunciar atajos para la pantalla actual
- F2: Ajustes del mod
- F3: Anunciar pantalla actual
- F4: Panel de amigos (desde menús) / Chat del duelo (durante duelos)
- F5: Buscar / iniciar actualización
- Ctrl+R: Repetir último anuncio

<h3>Duelos - Zonas</h3>

Tus zonas: C (Mano), G (Cementerio), X (Exilio), S (Pila), W (Zona de mando)
Zonas del oponente: Shift+G, Shift+X, Shift+W
Campo de batalla: B / Shift+B (Criaturas), A / Shift+A (Tierras), R / Shift+R (No criaturas)
Dentro de las zonas: Izquierda/Derecha para navegar, Arriba/Abajo para leer detalles de la carta, I para información extendida
Shift+Arriba/Abajo: Cambiar de fila en el campo de batalla

<h3>Duelos - Información</h3>

- T: Turno/Fase
- L: Puntos de vida
- V: Zona de información del jugador
- D / Shift+D: Cartas en biblioteca
- Shift+C: Cartas en mano del oponente
- M / Shift+M: Resumen de tierras tuyo / del oponente
- K: Info de contadores en la carta enfocada
- O: Registro de la partida (últimos anuncios del duelo)
- E / Shift+E: Temporizador tuyo / del oponente

<h3>Duelos - Objetivos y acciones</h3>

- Tab / Ctrl+Tab: Ciclar objetivos (todos / solo oponente)
- Intro: Seleccionar objetivo
- Espacio: Pasar prioridad, confirmar atacantes/bloqueadores, avanzar fase

<h3>Duelos - Full control y paradas de fase</h3>

- P: Alternar full control (temporal, se reinicia al cambiar de fase)
- Shift+P: Alternar full control bloqueado (permanente)
- Shift+Retroceso: Alternar pasar hasta acción del oponente (skip suave)
- Ctrl+Retroceso: Alternar saltar turno (forzar skip del turno entero)
- 1-0: Alternar paradas de fase (1=Mantenimiento, 2=Robar, 3=Primera fase principal, 4=Inicio de combate, 5=Declarar atacantes, 6=Declarar bloqueadores, 7=Daño de combate, 8=Fin de combate, 9=Segunda fase principal, 0=Paso final)

<h3>Duelos - Navegadores (Adivinación, Vigilar, Mulligan)</h3>

- Tab: Navegar por todas las cartas
- C/D: Saltar entre zonas superior/inferior
- Intro: Alternar colocación de carta

<h2>Solución de problemas</h2>

<h3>No hay voz después de iniciar el juego</h3>

- Asegúrate de que tu lector de pantalla esté en marcha antes de iniciar MTG Arena
- Verifica que `Tolk.dll` y `nvdaControllerClient64.dll` estén en la carpeta raíz de MTGA (el instalador los coloca automáticamente)
- Revisa el log de MelonLoader en tu carpeta MTGA (`MelonLoader\Latest.log`) por si hay errores

<h3>El juego se cuelga al arrancar o el mod no se carga</h3>

- Asegúrate de que MelonLoader esté instalado.
- Si el juego se actualizó recientemente, puede que haya que reinstalar MelonLoader o el mod. Ejecuta el instalador de nuevo.
- Comprueba que `AccessibleArena.dll` esté en la carpeta `Mods\` dentro de tu instalación de MTGA

<h3>El mod funcionaba pero dejó de hacerlo tras una actualización del juego</h3>

- Las actualizaciones de MTG Arena pueden sobrescribir archivos de MelonLoader. Ejecuta el instalador de nuevo para reinstalar tanto MelonLoader como el mod.
- Si el juego cambió su estructura interna significativamente, el mod puede necesitar una actualización. Busca nuevas releases en GitHub.

<h3>Los atajos de teclado no funcionan</h3>

- Asegúrate de que la ventana del juego esté enfocada (haz clic en ella o Alt+Tab)
- Pulsa F1 para comprobar si el mod está activo. Si oyes el menú de ayuda, el mod está funcionando.
- Algunos atajos solo funcionan en contextos específicos (los atajos de duelo solo funcionan durante un duelo)

<h3>Idioma incorrecto</h3>

- Pulsa F2 para abrir el menú de ajustes, luego usa Intro para cambiar de idioma

<h3>Windows advierte de que el instalador o la DLL son inseguros</h3>

El instalador y la DLL del mod no están firmados con certificado. Los certificados de firma de código cuestan varios cientos de euros al año, lo cual no es realista para un proyecto de accesibilidad gratuito. Por ello, Windows SmartScreen y algunos antivirus te avisarán la primera vez que ejecutes el instalador, o marcarán la DLL como "editor desconocido".

Para verificar que el archivo descargado coincide con el publicado en GitHub, cada release lista una suma SHA256 tanto para `AccessibleArenaInstaller.exe` como para `AccessibleArena.dll`. Puedes calcular el hash de tu archivo descargado y compararlo:

- PowerShell: `Get-FileHash <nombrearchivo> -Algorithm SHA256`
- Símbolo del sistema: `certutil -hashfile <nombrearchivo> SHA256`

Si el hash coincide con el de las notas de la release, el archivo es genuino. Para ejecutar el instalador saltando el aviso de SmartScreen, elige "Más información" y luego "Ejecutar de todos modos".

<h2>Reportar errores</h2>

Si encuentras un error, puedes publicar en el sitio donde encontraste el mod publicado, o [abrir una issue en GitHub](https://github.com/JeanStiletto/AccessibleArena/issues).

Incluye la siguiente información:

- Qué estabas haciendo cuando ocurrió el error
- Qué esperabas que pasara
- Qué pasó realmente
- Si quieres adjuntar un log del juego, cierra el juego y comparte el archivo de log de MelonLoader de tu carpeta MTGA:
  - WotC: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`
  - Steam: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\MelonLoader\Latest.log`

<h2>Problemas conocidos</h2>
El juego debería cubrir casi todas las pantallas, pero puede haber algunos casos límite que no funcionen del todo. PayPal bloquea a los usuarios ciegos con un captcha ilegal sin alternativa de audio, así que tienes que usar ayuda vidente u otros métodos de pago si quieres gastar dinero real en el juego.
Algunos eventos específicos pueden no estar del todo operativos. El drafting con jugadores reales tiene una pantalla de lobby aún no soportada, pero en quickdraft eliges cartas contra bots antes de enfrentarte a oponentes humanos, esto es funcional y un modo recomendado para todo el que le guste este tipo de experiencia. El modo Cube está sin tocar. Ni siquiera sé muy bien de qué va y cuesta muchos recursos en el juego. Así que lo haré si tengo tiempo o bajo petición.
El sistema de cosméticos del juego con emotes, mascotas, estilos de cartas y títulos solo está parcialmente soportado de momento.
El mod solo se ha probado en Windows con NVDA y JAWS y todavía depende de la biblioteca Tolk sin modificar. No puedo probar compatibilidad con Mac o Linux aquí, y bibliotecas multiplataforma como Prism no soportaban del todo las versiones antiguas de .NET de las que depende el juego por ahora. Por eso solo cambiaré a una biblioteca más amplia si hay gente que pueda ayudar a probar en otras plataformas o con lectores de pantalla asiáticos que Tolk sin modificar no soporta del todo. Así que no dudes en contactarme si quieres que trabaje en esto.

Para la lista actual de problemas conocidos, consulta [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

<h2>Descargos de responsabilidad</h2>
<h3>Otras accesibilidades</h3>

Este mod se llama Accessible Arena más que nada porque suena bien. Pero de momento es solo un mod de accesibilidad para lector de pantalla. Me interesa mucho cubrir más discapacidades con este mod, deficiencias visuales, discapacidades motoras, etc. Pero solo tengo experiencia en accesibilidad para lector de pantalla. Como persona totalmente ciega, por ejemplo, las cuestiones de color y tipografía me son totalmente abstractas. Así que si quieres algo así implementado, no dudes en contactarme si puedes describir claramente tus necesidades y estás dispuesto a ayudarme a probar los resultados.
Entonces estaré encantado de hacer el nombre de este mod un poco más fiel a la realidad.

<h3>Contacto con la empresa</h3>

Lamentablemente, no he conseguido obtener información fiable sobre el equipo de Arena ni contactos informales con desarrolladores. Por eso he decidido de momento omitir sus canales oficiales de comunicación. En 3 meses desarrollando y jugando nunca he topado con ningún sistema de protección contra bots, así que no creo que puedan detectarnos como usuarios de mods. Pero no quería asumir el riesgo de comunicarme por canales oficiales como una sola persona. Así que difunde la voz sobre el mod y construyamos una comunidad grande y valiosa. Entonces tendremos una posición mucho mejor si decidimos buscar contacto directo. No intentes escribirles sin haber hablado antes conmigo. Especialmente no les envíes peticiones de accesibilidad nativa ni de integración de mi mod en su código base. Ninguna de las dos cosas va a ocurrir en ningún caso.

<h3>Compras dentro del juego</h3>

Arena tiene mecánicas de dinero real y puedes comprar una moneda del juego. Esos métodos de pago son en su mayoría accesibles, excepto PayPal porque incluyeron protección captcha en su login. Puedes intentar desinstalar el mod para registrar el método de pago y pedir ayuda vidente, pero incluso eso es poco fiable por la pesadilla de accesibilidad que es el captcha, además implementado de forma absolutamente rota y chapucera por Wizards of the Coast.
Pero otros métodos de pago funcionan de forma estable. Yo y otros hemos probado comprar cosas en el juego y debería ser seguro usar el sistema. Pero es absolutamente posible que ocurran errores o incluso que el mod te engañe. Podría hacer clic en los elementos equivocados, mostrarte información errónea o incompleta, hacer cosas equivocadas por cambios internos de Arena. Podría probarlo, pero no puedo garantizar al 100% que no vayas a comprar las cosas equivocadas con tu dinero real. No asumiré responsabilidad por esto y, dado que no es un producto oficial de Arena, la empresa del juego tampoco lo hará. Por favor ni intentes conseguir reembolsos en ese caso, no te los darán.

<h3>Uso de IA</h3>

El código de este mod está creado al 100% con ayuda del agente Claude de Anthropic usando los modelos Opus: empezó con 4.5, la mayor parte del desarrollo ocurrió en 4.6, y los últimos pasos hacia el lanzamiento se hicieron en 4.7. Y gracias a mi mayor colaborador, también algo de Codex. Soy consciente de los problemas del uso de IA. Pero en una época en la que todos usan estas herramientas para cosas mucho más turbias mientras la industria del videojuego no ha podido darnos la accesibilidad que queremos en calidad ni en cantidad, aun así decidí usar las herramientas.

<h2>Cómo contribuir</h2>

Me encanta recibir contribuciones, y con [blindndangerous](https://github.com/blindndangerous) ya hay mucho trabajo útil de otra persona formando parte de este mod. Me interesan especialmente mejoras y correcciones para cosas que no puedo probar, como distintas configuraciones de sistema, arreglos para idiomas que no hablo, etc. Pero también acepto peticiones de funcionalidades. Antes de ponerte a trabajar en algo, revisa los problemas conocidos.

- Para guías generales de contribución, consulta [CONTRIBUTING.md](../CONTRIBUTING.md)
- Para ayudar con traducciones, consulta [CONTRIBUTING_TRANSLATIONS.md](CONTRIBUTING_TRANSLATIONS.md)

<h2>Créditos</h2>

Y ahora quiero agradecer a un montón de gente, porque afortunadamente esto no fui solo yo y la IA en una caja negra, sino toda una red alrededor mío, ayudando, dando fuerza, siendo simplemente sociales y amables.
Por favor envíame un DM si se me olvidó alguien o si quieres ser mencionado con otro nombre o no aparecer.

Primero, este trabajo se apoya mucho en el trabajo de otras personas que hicieron el trabajo pionero que yo solo he tenido que rehacer para Accessible Arena.
En cuanto a diseño, es Hearthstone Access de quien he podido heredar mucho, no solo porque es conocido por todo el que jugó, sino porque es un diseño de interfaz realmente bueno.
En cuanto a modding, quiero agradecer a los miembros del Discord de modding de Zax. No solo habéis descubierto todas las herramientas y procedimientos que yo solo tuve que instalar y usar. Me habéis enseñado todo lo que tengo que saber sobre modding con IA, bien directamente o a través de debates en público y ayudando a otros novatos. Además me habéis dado una plataforma y comunidad en la que mi proyecto y yo podemos existir.

Por enormes contribuciones de código quiero agradecer a [blindndangerous](https://github.com/blindndangerous), que también ha hecho muchísimo trabajo en este proyecto. A lo largo de la vida del proyecto creo que he recibido como 50 PRs y más de él sobre todo tipo de problemas, desde cosas pequeñas y molestas hasta propuestas mayores de interfaz y accesibilidad de pantallas enteras del juego.
Gracias también a Ahix, que creó [prompts de refactorización para grandes proyectos escritos con IA](https://github.com/ahicks92/llm-mod-refactoring-prompts) que ejecuté encima de mis propias refactorizaciones para asegurar calidad y mantenibilidad del código.

Por probar las betas, dar feedback e ideas, quiero agradecer a:
- Alfi
- Plüschyoda
- Firefly92
- Berenion
- [blindndangerous](https://github.com/blindndangerous)
- Toni Barth
- Chaosbringer216
- ABlindFellow
- SightlessKombat
- hamada
- Zack
- glaroc
- zersiax
- kairos4901
- [patricus3](https://github.com/patricus3)

Por las pruebas con personas videntes para entender flujos visuales y confirmar algunas cosas, quiero agradecer a:
- [mauriceKA](https://github.com/mauriceKA)
- VeganWolf
- Lea Holstein

<h3>Herramientas usadas</h3>

- Claude con todos los modelos incluidos
- MelonLoader
- Harmony para parcheo de IL
- Tolk para la comunicación con lector de pantalla
- ILSpy para descompilar el código del juego

<h2>Apoya a tu modder</h2>

Crear este mod no solo me ha aportado mucha diversión y empoderamiento, sino que también me ha costado realmente mucho tiempo y dinero real en suscripciones a Claude. Seguiré con ellas para trabajar en más mejoras y mantener el proyecto durante los próximos años.
Así que si estás dispuesto y puedes permitirte una donación única o incluso mensual, puedes echar un vistazo aquí.
Apreciaría mucho este reconocimiento a mi trabajo, y me da una base estable para seguir trabajando en Arena y, ojalá, en otros grandes proyectos en el futuro.

[Ko-fi: ko-fi.com/jeanstiletto](https://ko-fi.com/jeanstiletto)

<h2>Licencia</h2>

Este proyecto está licenciado bajo la GNU General Public License v3.0. Consulta el archivo LICENSE para más detalles.

<h2>Enlaces</h2>

- [GitHub](https://github.com/JeanStiletto/AccessibleArena)
- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [MTG Arena](https://magic.wizards.com/mtgarena)

<h2>Otros idiomas</h2>

[English](../README.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Italiano](README.it.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Polski](README.pl.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md)
