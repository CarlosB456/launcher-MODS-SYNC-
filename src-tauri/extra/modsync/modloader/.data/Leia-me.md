Mod Loader
==================

## O que é?

Mod Loader é um plugin para Grand Theft Auto III, Vice City e San Andreas que adiciona uma forma fácil e amigável de instalar e desinstalar modificações no jogo, como se o jogo tivesse suporte oficial a mods. Nenhuma alteração é feita nos arquivos originais do jogo; tudo é injetado em tempo de execução!

A utilização é tão simples quanto colocar os arquivos do mod no diretório modloader/. Desinstalar é igualmente fácil: apague os arquivos do mod e pronto. Trocar mods com o jogo aberto? Com o Mod Loader você pode!

Ainda com dúvidas? Confira este [vídeo](https://www.youtube.com/watch?v=TvRpQa8dJ7E) do Ivy.

## Instalando o Mod Loader

### GTA San Andreas

  Mod Loader requer um [ASI Loader](http://www.gtagarage.com/mods/show.php?id=21709). 
  
  Extraia o *modloader.asi* e a pasta *modloader* para o diretório do jogo.

### GTA Vice City ou GTA III:

  Mod Loader requer o [Ultimate ASI Loader](https://github.com/ThirteenAG/Ultimate-ASI-Loader/releases).
  
  Extraia o *modloader.asi* para a pasta *scripts/* e a pasta *modloader* para o diretório do jogo.


## Instalando modificações

Extraia o conteúdo da modificação em uma pasta dentro do diretório *modloader*.

Isso significa que os seguintes são métodos válidos de instalação:

 + modloader/nsx/infernus.dff
 + modloader/nsx/outra pasta/infernus.dff

Entretanto, o seguinte **NÃO** é um método válido:

 - modloader/infernus.dff 
 - modloader/.data/infernus.dff


## Desinstalando modificações

Ainda mais fácil, apenas delete o conteúdo da modificação do diretório *modloader*.

Se você só quiser desabilitar a modificação por um instante, vá para o menu in-game para desabilitá-la, ou edite o *modloader.ini* manualmente. 

## Destaques

- Não substitua **NENHUM** arquivo original. Nunca. Sério.
- Deixe que o Mod Loader tome conta de tudo.
- Mixagem de arquivos data. Você pode, por exemplo, ter 70 arquivos handling.cfg no modloader e eles irão funcionar perfeitamente.
- Leitura de arquivos readme. Não é necessário se preocupar em copiar a linha data do arquivo readme para o arquivo data; o Mod Loader faz isso por você também!
- Mods recarregáveis. Mude ou adicione arquivos enquanto o jogo está rodando e veja o resultado imediatamente!
- Suporte para linha de comando. Veja *modloader/.data/Command Line Arguments.md*.
- Menu em jogo para configurações. Vá para *Options > Mod Configuration*.

### Encontrou uma falha?

Encontrou um bug? Reporte em [nosso issue tracker](https://github.com/thelink2012/modloader/issues).

Por favor, inclua o arquivo *modloader/modloader.log* criado logo após o crash e os passos para reproduzir.

### Executáveis suportados

Versões suportadas:

 + GTA III 1.0
 + GTA VC 1.0
 + GTA SA 1.0 US
 + GTA SA 1.0 EU

## Download

Você pode fazer o download da última versão do Mod Loader em:

 * [GTA Garage](http://www.gtagarage.com/mods/show.php?id=25377) — última versão estável
 * [GitHub](https://github.com/thelink2012/modloader/releases) — últimas versões, incluindo instáveis

## Código-Fonte

Mod Loader é um projeto open source, sinta-se livre para aprender e contribuir.
O código fonte está sob a MIT License, se ligue no [GitHub](https://github.com/thelink2012/modloader/).

## Colaboradores

Originalmente desenvolvido por LINK/2012 com contribuições, suporte e testes de

- Andryo
- ArtututuVidor$
- CookiePLMonster
- Fabio3rs
- Gramps
- Junior_Djjr
- JNRois12
- LINK/2012
- rokuniichi
- Silent
- ThirteenAG
- TheComputerGuy96
- TJGM

e muitas outras pessoas.
