# Catálogo de códigos de requisitos

Foram obtidos documentos de fontes oficiais em 21/10/2026 e extraídos os códigos numéricos. Foram acrescentados títulos curtos em português, resumidos a partir dos tópicos dos documentos, sem descrições nem reprodução do texto integral dos requisitos.

| Cadastro | Edição usada | Quantidade | Escopo |
|---|---|---:|---|
| ISO 9001:2015 | 2015 | 73 | Seções 4–10 e subseções presentes no sumário da prévia oficial |
| ISO 14001 | 2015 | 45 | Seções 4–10 e subseções presentes no sumário da prévia oficial |
| ISO 45001 | 2018 | 50 | Seções 4–10 e subseções presentes no sumário da prévia oficial |
| ANSN 1.16 | Abril/2000, Portaria CNEN 17/00, documento publicado como ANSN 1.16 | 171 | Subseções 4.1–4.13 e seus desdobramentos identificados no PDF oficial |

Total: 339 códigos. As seções principais das ISO também são opções selecionáveis. Os sumários não mostram necessariamente todos os níveis de subdivisão; o catálogo não representa a transcrição integral dos requisitos. Não foram substituídas as edições usadas pela empresa pelas versões mais recentes. As edições usadas estão visíveis na interface; requisitos adicionais podem ser cadastrados manualmente.

ASME III e Secretaria Naval foram excluídas da pré-carga por orientação do usuário; seus cadastros permanecem disponíveis para uso manual.

## Fontes

- [Prévia ISO 9001:2015 — ANSI](https://webstore.ansi.org/preview-pages/ISO/preview_ISO+9001-2015.pdf)
- [Prévia ISO 14001:2015 — ANSI](https://webstore.ansi.org/preview-pages/ISO/preview_ISO+14001-2015.pdf)
- [Prévia ISO 45001:2018 — ANSI](https://webstore.ansi.org/preview-pages/ISO/preview_ISO+45001-2018.pdf)
- [ANSN 1.16 — portal gov.br/CNEN](https://www.gov.br/cnen/pt-br/acesso-rapido/normas/grupo-1/ansn-1-16-garantia-da-qualidade-para-a-seguranca-de-usinas-nucleoeletricas-e-outras-instalacoes.pdf)

O catálogo versionado é `static/requirements-catalog.json`, com códigos, edições, URLs, data de obtenção e SHA-256 dos PDFs. Os PDFs e textos extraídos não são redistribuídos no repositório.

## Pré-carga e atualização

O servidor carrega o catálogo no SQLite ao inicializar a conexão. A demonstração incorpora o mesmo catálogo e importa os códigos no armazenamento local do navegador. Chave de duplicidade: norma e código, sem diferenciar maiúsculas/minúsculas. Registros existentes são preservados, inclusive identificadores e dados manuais. Repetir a inicialização não duplica códigos. O marcador de importação registra a versão de cada lote.

Cada código possui título. Nos subitens ANSN sem cabeçalho próprio, o título usa o tópico superior e identifica o item; isso não é apresentado como título normativo literal. `scripts/requirement_titles_pt.json` registra os resumos usados na geração. A migração preenche títulos ausentes, preserva IDs e títulos manuais, e não duplica requisitos.

A pré-carga foi executada no banco desta instância e verificada: 339 registros; segunda execução adicionou zero. Os testes também verificam a importação em banco vazio, preservação de um registro manual, filtros por norma e seleção em plano.

Para reproduzir a extração, baixe os quatro PDFs oficiais em uma pasta, usando os nomes 9001.pdf, 14001.pdf, 45001.pdf e ansn116.pdf. Gere os respectivos .txt com `pdftotext -layout`. Execute `python scripts/build_requirement_catalog.py CAMINHO_DA_PASTA`, confira o diff e as quantidades, rode os testes e publique a prévia novamente.
