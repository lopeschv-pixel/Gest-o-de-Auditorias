# Importação dos requisitos normativos — pendente

Ainda não foram adicionados requisitos obtidos da internet. As tentativas de acesso às fontes abaixo retornaram bloqueio de rede HTTP 403 (CONNECT), antes de receber os documentos. Os cadastros existentes foram preservados. Não foram inseridos itens estimados ou textos de normas sem conferência.

Fontes consultadas em 21/10/2026:

- ISO 9001:2015: https://www.iso.org/standard/62085.html
- ISO 14001: https://www.iso.org/standard/60857.html
- ISO 45001: https://www.iso.org/standard/63787.html
- ANSN/CNEN 1.16: https://www.gov.br/cnen/pt-br/acesso-rapido/normas/grupo-1/grupo1-nn116.pdf (endereço tentado; documento e revisão não confirmados).
- Catálogo ASME: https://www.asme.org/codes-standards/
- Prévia oficial de editora ANSI: https://webstore.ansi.org/preview-pages/ISO/preview_ISO+9001-2015.pdf

Foi salvo no rascunho do ambiente o acesso a www.iso.org, committee.iso.org, www.gov.br, www.asme.org e webstore.ansi.org, preservando os presets existentes. A configuração precisa ser salva/publicada no ambiente antes de retentar os downloads. Isso não altera a hospedagem no GitHub Pages.

Antes da importação, identificar as edições aplicáveis de ISO 14001 e ISO 45001, a edição/divisão/subseção de ASME III e o código/título completo de “Secretaria Naval”. Também confirmar a revisão aplicável da ANSN 1.16. Uma norma cadastrada pelo usuário pode não corresponder a um documento normativo identificável pelo nome.

O cadastro agora exige apenas norma e código do requisito. A futura importação deve armazenar códigos confirmados, associados à norma/edição aplicável, sem descrição obrigatória e sem substituir os registros cadastrados. No servidor, isso é feito por POST /api/requirements; na demonstração Pages os dados são locais ao navegador e precisam ser pré-carregados na versão publicada usando um catálogo verificado. Não afirmar que o SQLite centraliza dados do Pages: são modos diferentes.
