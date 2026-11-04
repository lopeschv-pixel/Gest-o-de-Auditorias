# Carga inicial de normas e requisitos

A carga (`src/NuClaud.Core/Dados/SementeCatalogo.cs`) é **idempotente**: inclui apenas normas ausentes e só carrega
requisitos em normas que ainda não têm nenhum. Foram carregados somente **números e títulos de seções** (dois ou três níveis),
sem nenhum trecho do texto das normas. Itens sem título próprio (ex.: 4.4.1, 4.4.2, 7.1.5.1) e itens com quatro níveis
(ex.: 6.1.2.1 da ISO 45001) não foram incluídos, conforme a regra de níveis.

| Norma | Edição registrada | Requisitos | Situação |
|---|---|---|---|
| ISO 9001:2015 | ABNT NBR ISO 9001:2015 (ISO 9001:2015) | 66 | Carregada; títulos do sumário da edição brasileira |
| ISO 14001 | ABNT NBR ISO 14001:2015 (ISO 14001:2015) | 38 | Carregada |
| ISO 45001 | ABNT NBR ISO 45001 (ISO 45001:2018) | 43 | Carregada |
| ANSN 1.16 | não definida | 0 | **Pendente** — cadastro manual |
| ASME III | não definida | 0 | Cadastro manual (edição/divisão a definir) |
| Secretaria Naval | não definida | 0 | Cadastro manual (documento a definir) |

## Fontes e conferência
- Títulos das seções conforme o sumário das edições ABNT NBR. A conferência em fontes públicas foi parcial
  (ex.: guias técnicos que citam 7.1.5 “Recursos de monitoramento e medição”, 7.1.6 “Conhecimento organizacional”,
  8.5.6 “Controle de mudanças”, 10.3 “Melhoria contínua”; e itens 6.1.2, 6.1.3 e 8.2 da ISO 14001).
- **Pendência:** recomenda-se conferir os títulos contra o exemplar licenciado da NUCLEP, em especial na ISO 45001
  (7.5.2, 8.1.4 e 9.1.2), onde a redação brasileira pode variar. A ISO 9001 tem a Emenda 1:2024 (mudanças climáticas
  em 4.1 e 4.2), que não altera numeração nem títulos. Títulos podem ser corrigidos na tela “Cadastrar normas e requisitos”
  sem afetar auditorias já registradas.
- **ANSN 1.16:** corresponde à antiga **CNEN NN 1.16** — “Garantia da Qualidade para a Segurança de Usinas Nucleoelétricas
  e Outras Instalações” (Resolução CNEN 15/99, Portaria CNEN 17/00). O texto oficial (gov.br) não pôde ser acessado a partir do
  ambiente de desenvolvimento (rede bloqueada), por isso nenhum requisito foi associado a essa norma para não carregar títulos
  sem verificação. Fontes secundárias indicam seções como 4.5 “Controle de Projeto” e 4.6 “Controle de Aquisições”, mas a
  estrutura completa e a versão ANSN vigente precisam ser confirmadas e cadastradas manualmente.
- A demonstração inclui um requisito **fictício** em ASME III (“1.1 — Exemplo…”) apenas no banco de testes.
