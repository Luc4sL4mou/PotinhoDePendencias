# Plano de Qualidade: Potinho de Pendências

App desktop Windows (C# WinForms, .NET 10). Dados em `%APPDATA%\PotinhoDePendencias\dados.json`.
Lógica em `src/Pote.cs` (sem UI), testada por `PotinhoDePendencias.exe --teste` (saída 0 = passou).

## Fluxos críticos e critérios de aceite

```gherkin
# language: pt
Funcionalidade: Potinho de pendências

  Cenário: Pôr uma pendência no pote
    Dado que o pote está vazio
    Quando escrevo "Lavar a louça", escolho 20 XP e aperto Enter
    Então o papel com "Lavar a louça" se dobra e cai dentro do pote
    E a aba mostra "Pote (1)"
    E o campo de texto fica vazio e com foco

  Cenário: Sortear
    Dado que o pote tem 3 pendências
    Quando clico em "Sortear!"
    Então a tampa abre, um papelzinho sobe e se desdobra acima do pote mostrando a pendência
    E "Concluir", "Devolver ao pote" e "Remover" agem sobre esse papel

  Cenário: Escolher um papel
    Quando clico num papelzinho dentro do pote
    Então ele sai e se desdobra (o que estava na mão volta dobrado para o pote)

  Cenário: Sortear com pote vazio
    Dado que o pote está vazio
    Quando clico em "Sortear!"
    Então vejo "O pote está vazio"

  Cenário: Concluir e subir de nível
    Dado que tenho 90 XP e um papel de 20 XP na mão
    Quando clico em "Concluir" e confirmo
    Então vejo "SUBIU PARA O NÍVEL 2"
    E a pendência aparece no topo da aba "Arquivo" com a data de hoje

  Cenário: Dados persistem
    Dado que adicionei e concluí pendências
    Quando fecho e abro o app
    Então pote, arquivo, nível e XP continuam iguais
```

Regra de nível: subir do nível N para o N+1 custa 100×N XP. XP total = soma do arquivo.

## Definição de pronto
- `dotnet publish` sem avisos novos; `--teste` sai com 0.
- Fluxos acima conferidos à mão no exe da raiz (não há E2E automatizado em WinForms aqui).
- Toda ação dá retorno (mensagem ou lista atualizada); remover e concluir pedem confirmação.
- Usável só com teclado: Tab entre campos, Enter adiciona, Enter/espaço nos botões.
- Exe da raiz e atalho atualizados.

## Metas
- Lógica (`Pote.cs`) com todos os ramos cobertos por `Testes.cs`; UI sem meta de cobertura.
- Funções ≤ ~50 linhas, arquivos ≤ ~400 linhas.
- Nunca perder dados: gravação via `.tmp` + troca; JSON ilegível impede abrir em vez de sobrescrever.

## Pirâmide
Unitários da lógica (`--teste`) → teste manual dos 5 cenários antes de cada versão.
Se a UI crescer, considerar FlaUI para E2E.
