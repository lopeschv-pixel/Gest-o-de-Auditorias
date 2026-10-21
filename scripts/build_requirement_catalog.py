"""Extract numeric clause references from downloaded authoritative PDF text.

Usage: python scripts/build_requirement_catalog.py /tmp/nuclaud-sources
Requires the official PDFs and pdftotext outputs; does not redistribute texts.
"""
import hashlib
import json
from pathlib import Path
import re
import sys

source_dir = Path(sys.argv[1])
root = Path(__file__).resolve().parent.parent
batches = []
for name, key, edition in [('ISO 9001:2015','9001','2015'),('ISO 14001','14001','2015'),('ISO 45001','45001','2018')]:
    text = (source_dir / (key+'.txt')).read_text()
    text = text[text.index('Contents'):]
    stop = re.search(r'\n\s*Foreword\s*\n', text)
    if not stop:
        raise ValueError('Sumário não delimitado: '+name)
    codes = set(re.findall(r'^\s*((?:[4-9]|10)(?:\.\d+)*)\s+[A-Za-z]', text[:stop.start()], re.M))
    if not {'4.1','10.3'} <= codes:
        raise ValueError('Sumário incompleto: '+name)
    batches.append(dict(norm=name,edition=edition,source=f'https://webstore.ansi.org/preview-pages/ISO/preview_ISO+{key}-{edition}.pdf',source_sha256=hashlib.sha256((source_dir / (key+'.pdf')).read_bytes()).hexdigest(),scope='Códigos das seções 4 a 10 e subseções presentes no sumário da prévia oficial; não inclui subdivisões que não aparecem no sumário.',codes=sorted(codes,key=lambda c:tuple(map(int,c.split('.'))))))
text = (source_dir / 'ansn116.txt').read_text()
codes = set(re.findall(r'^\s*(4(?:\.\d+)+)(?:\.(?=[^\d])|(?=\s|[A-Z]))', text, re.M))
if not {'4.1','4.13.2.2','4.6.3.1','4.5.4.3'} <= codes:
    raise ValueError('Extração ANSN incompleta')
batches.append(dict(norm='ANSN 1.16',edition='Abril/2000 — Portaria CNEN 17/00; documento atualmente publicado como ANSN 1.16',source='https://www.gov.br/cnen/pt-br/acesso-rapido/normas/grupo-1/ansn-1-16-garantia-da-qualidade-para-a-seguranca-de-usinas-nucleoeletricas-e-outras-instalacoes.pdf',source_sha256=hashlib.sha256((source_dir / 'ansn116.pdf').read_bytes()).hexdigest(),scope='Subseções numéricas 4.1 a 4.13 e seus desdobramentos identificados no PDF oficial.',codes=sorted(codes,key=lambda c:tuple(map(int,c.split('.'))))))
title_tables = json.loads((root / 'scripts/requirement_titles_pt.json').read_text())
for batch in batches:
    table = title_tables[batch['norm']]
    batch['titles'] = {}
    batch['inherited_titles'] = []
    for code in batch['codes']:
        parent = code
        while parent not in table and '.' in parent:
            parent = parent.rsplit('.', 1)[0]
        if parent not in table:
            raise ValueError('Título não encontrado: '+batch['norm']+' '+code)
        batch['titles'][code] = table[parent] if parent == code else f'{table[parent]} — item {code}'
        if parent != code:
            batch['inherited_titles'].append(code)
    batch['title_note'] = 'Títulos curtos em português, resumidos a partir dos tópicos da fonte. Subitens sem título próprio herdam o tópico superior, com o número do item.'
for batch in batches:
    batch['import_key'] = hashlib.sha256(json.dumps(batch,sort_keys=True,ensure_ascii=False).encode()).hexdigest()
catalog = dict(retrieved_on='2026-10-21',excluded_norms=['ASME III','Secretaria Naval'],batches=batches)
(root / 'static/requirements-catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n')
for batch in batches:
    print(batch['norm'],batch['edition'],len(batch['codes']))
