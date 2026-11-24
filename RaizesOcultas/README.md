# Raízes Ocultas

Protótipo acadêmico de realidade virtual desenvolvido por Fabrício Giordani Antonini e Raphael Frey Machado. O jogador explora um sítio fictício, escava três artefatos, limpa as peças com um pincel e consulta informações sobre memória e cultura afrodescendente.

O cenário está salvo em `Assets/RaizesOcultas/Gerado/RaizesOcultas.unity`, com as ferramentas, os materiais, os painéis, o rig XR e suas configurações.

## Abrir e jogar

1. Instale **Unity 6000.6.8f1**, a versão registrada em `ProjectSettings/ProjectVersion.txt`.
2. No Unity Hub, adicione e abra esta pasta, que contém `Assets`, `Packages` e `ProjectSettings`.
3. Aguarde a restauração dos pacotes e a importação inicial. A cena **RaizesOcultas** abre automaticamente.
4. Aperte **Play**, dê foco à aba **Game** e clique em **Iniciar pesquisa** no painel de entrada.

Os pacotes estão definidos em `Packages/manifest.json` e `Packages/packages-lock.json`. A primeira abertura precisa de acesso à internet para restaurá-los.

## Testar com teclado e mouse

O **XR Device Simulator** está configurado para iniciar automaticamente no Editor. O painel do simulador mostra o dispositivo selecionado e os controles disponíveis.

| Ação | Controle |
|---|---|
| Alternar entre dispositivos simulados | **Tab** |
| Controlar a mão esquerda temporariamente | Segurar **Shift esquerdo** |
| Controlar a mão direita temporariamente | Segurar **Espaço** |
| Alternar o controle contínuo da mão esquerda ou direita | **T** ou **Y** |
| Manipular a cabeça | Botão direito do mouse |
| Alternar para o modo de rotação | Segurar **Ctrl esquerdo** |
| Apertar a empunhadura do controle selecionado | **G** |
| Apertar o gatilho do controle selecionado | Botão esquerdo do mouse |
| Reiniciar a pose do dispositivo selecionado | **V** |
| Abrir ou fechar o diário de campo | **J** |

Aproxime a mão de uma ferramenta para pegá-la com a empunhadura. Faça três cavadas em cada quadra, retirando a ponta da pá da área entre as passadas. Depois passe o pincel nas manchas. Ao concluir a limpeza, a peça pode ser manipulada e seu painel de leitura aparece. O catálogo final é liberado após as três descobertas.

## Testar no Quest

O projeto usa OpenXR e tem configurações Android para Meta Quest. Para gerar um APK, instale os módulos Android Build Support, Android SDK & NDK Tools e OpenJDK da mesma instalação do Editor. O simulador está limitado ao Editor nas configurações do projeto.

## Organização

| Caminho | Conteúdo |
|---|---|
| `Assets/RaizesOcultas/Gerado/RaizesOcultas.unity` | Cena principal montada |
| `Assets/RaizesOcultas/Gerado/Materiais` | Materiais usados no cenário |
| `Assets/RaizesOcultas/Scripts` | Mecânicas de escavação, limpeza, descoberta e diário |
| `Assets/RaizesOcultas/Audio` | Efeitos sonoros e ambiente |
| `Assets/RaizesOcultas/Editor/RaizesProjectStartup.cs` | Abertura da cena salva após a importação |
| `Assets/RaizesOcultas/Editor` | Ferramentas usadas na construção e na configuração do protótipo |
| `Assets/VRTemplateAssets` e `Assets/Samples` | Assets e exemplos XR utilizados pelo projeto |
| `ProjectSettings/EditorBuildSettings.asset` | Lista de cenas, com RaizesOcultas habilitada na primeira posição |