> Referência do módulo de origem. Para executar a plataforma integrada Domus, consulte o [README principal](../../../README.md). O único Compose está em `infra/compose.yaml`.

# Produção de manifestos de automação

## Localização e responsabilidade

O manifesto de produção pertence à própria automação. No repositório `automations`,
a estrutura é:

```text
src/pending-records/
  automation.json
  package.json
  build.cjs
  scripts/build-linux.js
  scripts/build-windows.js
  scripts/build-macos.js
```

Edite `src/<id>/automation.json` juntamente com o código, argumentos e recursos da
automação. Inclua o ficheiro no mesmo commit quando alterar o contrato de execução.
O portal lê os manifestos dos pacotes publicados; não mantém uma cópia de produção.
Os JSON em `tests/fixtures/manifests/` no portal são apenas dados de teste.

Existem dois ficheiros com funções distintas:

- **Fonte:** `src/<id>/automation.json`, mantido pelo autor da automação.
- **Artefacto:** `dist/<pacote>/automation.json`, produzido pelo build e incluído no
  ZIP, junto do executável. Não o edite manualmente depois de construir o pacote.

## Definir o contrato

O [schema v1](../../schemas/automation.schema.json) e o validador do portal
são a referência para os campos obrigatórios e restrições. Para uma automação nova,
use um manifesto existente como ponto de partida e reveja todos os campos:

| Campo | Como produzir |
| --- | --- |
| `schemaVersion` | Use `1` para o contrato atual. |
| `id` | Identificador estável, em minúsculas com hífen, igual à pasta da automação. |
| `version` | Versão numérica `major.minor.patch`; o build escreve a versão real do artefacto. |
| `name`, `description`, `publisher` | Nome legível, comportamento concreto e autor. |
| `platform.os`, `platform.arch` | Plataforma do executável distribuído; no Linux use `linux` e `x64` ou `arm64`. |
| `execution.executable` | Caminho relativo do executável dentro do pacote. |
| `execution.arguments` | Argumentos fixos, sem credenciais ou dados reais. |
| `execution.homeEnvironmentVariable` | Variável que a automação usa para a sua pasta de trabalho. |
| `execution.packagePaths` | Variáveis cujos valores apontam para recursos relativos ao pacote, como browsers. |
| `execution.workspaceResources` | Recursos do pacote a copiar para cada workspace; não podem substituir inputs. |
| Restantes campos de `execution` | Descreva isolamento, browser, protocolo de estado e argumento da pasta de estado conforme o schema. |
| `authentication` | Indique o modo de autenticação e se há CAPTCHA. |
| `inputs` | Declare cada input, tipo, obrigatoriedade, limites e binding para a interface real da automação. |
| `outputs` | Declare caminhos relativos e descrições dos resultados esperados. |
| `effects` | Indique corretamente alteração de inputs, publicação de comentários e pedidos pagos. |

Os tipos de input são `file`, `string`, `integer` e `secret`. Ficheiros podem declarar
extensões e colunas Excel; inteiros podem ter mínimo/máximo. Um input liga-se a um
argumento ou a uma chave de ficheiro de ambiente. Segredos usam `envFileKey`, nunca
argumentos ou valores predefinidos. Identificadores e bindings devem ser únicos.

Não use caminhos absolutos, `..`, letras de unidade ou separadores Windows nos
caminhos do pacote. As referências a executáveis e recursos devem existir no ZIP.
Não declare opções que o executável não suporta. Não inclua tokens, credenciais,
Excel de trabalho, downloads reais ou backups nos pacotes publicados.

## Validar o manifesto fonte

Com os dois repositórios lado a lado e as dependências do portal instaladas, execute
na raiz de `automations-portal`:

```sh
node infra/scripts/automations/validate-packages.mjs ../automations/src/pending-records/automation.json
node infra/scripts/automations/validate-packages.mjs ../automations/src/*/automation.json
```

Adapte os caminhos à localização dos checkouts. A validação verifica o schema e
regras adicionais de bindings, segredos e caminhos. Sem argumentos, `npm --prefix tests run check`
valida apenas as fixtures do portal; não valida as fontes de outro repositório.

## Produzir o manifesto do artefacto

Nos builds atuais do repositório `automations`, a versão vem de `APP_VERSION` quando
for definida; caso contrário, vem de `package.json`. Linux e macOS também escrevem
no manifesto a plataforma nativa e ajustam o nome do executável. Windows conserva
a configuração Windows do manifesto fonte e substitui a versão.

Para o servidor Docker, construa num runner Linux nativo da arquitetura pretendida.
O build Linux exige Node.js 22.13+, dependências npm, `curl`, `tar` e `zip`, além das
dependências de execução do browser. Consulte também as configurações dos runners
no repositório das automações. Na pasta da automação:

```sh
cd src/pending-records
npm --prefix tests ci
npm --prefix tests run build
npm --prefix tests run check
APP_VERSION=1.2.3 npm --prefix tests run build:linux
```

O exemplo produz `dist/PendingRecords-linux-amd64-v1.2.3.zip` num runner x64 ou
`dist/PendingRecords-linux-arm64-v1.2.3.zip` num runner ARM64. O manifesto interno
terá `version: "1.2.3"`, `platform.os: "linux"`, a arquitetura correspondente e
`execution.executable: "PendingRecords"`. Alterar apenas `platform` não converte um
executável Windows em Linux.

Para um produtor de pacotes que não usa estes builders, prepare primeiro um
manifesto com a plataforma e caminhos corretos e use a ferramenta genérica a partir
da raiz do portal:

```sh
node infra/scripts/automations/prepare-package.mjs /path/to/automation.json /path/to/built-package 1.2.3
```

Esta ferramenta valida o contrato e escreve `automation.json` de forma atómica,
substituindo **apenas a versão**. Não compila o executável nem converte a plataforma.

## Verificar e publicar

Antes de criar a release:

1. Valide também o manifesto gerado com `infra/scripts/automations/validate-packages.mjs`, passando
   o caminho do ficheiro em `dist/<pacote>/automation.json`.
2. Confirme que o ZIP contém um único manifesto junto do executável, diretamente
   na raiz ou dentro de uma única pasta exterior.
3. Confirme que ID, versão, plataforma e arquitetura correspondem ao binário,
   ao nome do ZIP e à tag de release.
4. Teste o pacote com inputs sintéticos, incluindo resultados e efeitos declarados.
5. Publique o ZIP numa release estável com tag como `pending-records-v1.2.3`.
   O commit da tag deve conter `src/pending-records/automation.json`, usado pelo
   catálogo para obter o nome e a descrição antes de instalar.
6. Atualize o Catálogo no portal e confirme que aparece **Instalar** ou **Atualizar**.

O catálogo filtra drafts, prereleases e plataformas incompatíveis. O worker valida
novamente o manifesto, os caminhos e a identidade durante a instalação. Instalar
com sucesso não substitui um teste de execução com as dependências reais do servidor.
