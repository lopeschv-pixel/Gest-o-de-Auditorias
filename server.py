import json
import os
import sqlite3
import access
import hashlib
import secrets
from urllib.parse import urlsplit
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT = Path(__file__).parent
DB = Path(os.environ.get('NUCLAUD_DB', ROOT / 'data' / 'nuclaud.db'))
PRELOAD_REQUIREMENTS = True
CATALOG_PATH = ROOT / 'static' / 'requirements-catalog.json'
NORMS = ['ISO 9001:2015', 'ANSN 1.16', 'ISO 14001', 'ISO 45001', 'ASME III', 'Secretaria Naval']

def connection():
    DB.parent.mkdir(parents=True, exist_ok=True)
    con = sqlite3.connect(DB)
    con.execute('PRAGMA foreign_keys=ON')
    con.execute('CREATE TABLE IF NOT EXISTS audits (id INTEGER PRIMARY KEY, payload TEXT NOT NULL)')
    con.execute('CREATE TABLE IF NOT EXISTS findings (id INTEGER PRIMARY KEY, audit_id INTEGER NOT NULL REFERENCES audits(id), payload TEXT NOT NULL)')
    con.execute('CREATE TABLE IF NOT EXISTS areas (id INTEGER PRIMARY KEY, name TEXT NOT NULL UNIQUE)')
    con.execute('CREATE TABLE IF NOT EXISTS counters (kind TEXT, year INTEGER, sequence INTEGER NOT NULL, PRIMARY KEY(kind,year))')
    con.execute('CREATE TABLE IF NOT EXISTS auditors (id INTEGER PRIMARY KEY, payload TEXT NOT NULL)')
    con.execute('CREATE TABLE IF NOT EXISTS requirements (id INTEGER PRIMARY KEY, payload TEXT NOT NULL)')
    con.execute('CREATE TABLE IF NOT EXISTS macroprocesses (id INTEGER PRIMARY KEY,name TEXT NOT NULL UNIQUE)')
    con.execute('CREATE TABLE IF NOT EXISTS processes (id INTEGER PRIMARY KEY,macroprocess_id INTEGER NOT NULL REFERENCES macroprocesses(id),name TEXT NOT NULL,UNIQUE(macroprocess_id,name))')
    access.schema(con)
    con.execute('CREATE TABLE IF NOT EXISTS norms (id INTEGER PRIMARY KEY, name TEXT NOT NULL UNIQUE)')
    for name in NORMS:
        con.execute('INSERT OR IGNORE INTO norms(name) VALUES (?)', (name,))
    for aid, payload in con.execute('SELECT id,payload FROM auditors').fetchall():
        auditor = json.loads(payload)
        changed = False
        for norm, role in auditor.get('qualifications', {}).items():
            if isinstance(role, str):
                auditor['qualifications'][norm] = ['leader', 'auditor'] if role == 'leader' else [role]
                changed = True
        if changed:
            con.execute('UPDATE auditors SET payload=? WHERE id=?', (json.dumps(auditor), aid))
    # Preserve existing records and assign an identity only when missing.
    for aid, payload in con.execute('SELECT id,payload FROM audits ORDER BY id').fetchall():
        audit = json.loads(payload)
        if not audit.get('code'):
            audit.setdefault('audit_type', 'I')
            audit['code'] = next_code(con, audit['audit_type'], audit['year'])
            audit.pop('title', None)
            con.execute('UPDATE audits SET payload=? WHERE id=?', (json.dumps(audit), aid))
        for name in audit.get('area_names', [audit['area']] if audit.get('area') else []):
            con.execute('INSERT OR IGNORE INTO areas(name) VALUES (?)', (name,))
    if PRELOAD_REQUIREMENTS:
        seed_requirements(con)
    con.commit()
    return con


def requirement_sources():
    return [{k:v for k,v in batch.items() if k != 'codes'} | {'count':len(batch['codes'])} for batch in json.loads(CATALOG_PATH.read_text())['batches']]


def seed_requirements(con):
    con.execute('CREATE TABLE IF NOT EXISTS requirement_imports (norm TEXT PRIMARY KEY, import_key TEXT NOT NULL)')
    batches = json.loads(CATALOG_PATH.read_text())['batches']
    added = 0
    for batch in batches:
        previous = con.execute('SELECT import_key FROM requirement_imports WHERE norm=?', (batch['norm'],)).fetchone()
        if previous and previous[0] == batch['import_key']:
            continue
        existing = {(json.loads(payload)['norm'].casefold(), json.loads(payload)['code'].strip().casefold()): (rid,json.loads(payload)) for rid,payload in con.execute('SELECT id,payload FROM requirements')}
        for code in batch['codes']:
            key = (batch['norm'].casefold(), code.casefold())
            if key in existing:
                rid, current = existing[key]
                if not current.get('title') and batch.get('titles', {}).get(code):
                    current['title'] = batch['titles'][code]
                    con.execute('UPDATE requirements SET payload=? WHERE id=?', (json.dumps(current),rid))
                continue
            payload = dict(norm=batch['norm'], code=code, title=batch.get('titles', {}).get(code, ''), source=batch['source'], edition=batch['edition'], source_sha256=batch['source_sha256'])
            con.execute('INSERT INTO requirements(payload) VALUES (?)', (json.dumps(payload),))
            existing[key] = (None, payload)
            added += 1
        con.execute('INSERT INTO requirement_imports(norm,import_key) VALUES (?,?) ON CONFLICT(norm) DO UPDATE SET import_key=excluded.import_key', (batch['norm'],batch['import_key']))
    return added


def next_code(con, kind, year):
    con.execute('INSERT INTO counters(kind,year,sequence) VALUES (?,?,1) ON CONFLICT(kind,year) DO UPDATE SET sequence=sequence+1', (kind, int(year)))
    number = con.execute('SELECT sequence FROM counters WHERE kind=? AND year=?', (kind, int(year))).fetchone()[0]
    return f'AUDI-{kind}-{number:03d}-{int(year)}'


def check_area(con, data):
    names = data.get('area_names', [data.get('area')])
    if not isinstance(names, list) or not names or any(not isinstance(n, str) or not con.execute('SELECT 1 FROM areas WHERE name=?', (n,)).fetchone() for n in names):
        raise ValueError('Selecione uma ou mais áreas cadastradas')
    if len(set(names)) != len(names):
        raise ValueError('Áreas duplicadas')
    data['area_names'] = names
    data['area'] = ' · '.join(names)


def process_catalog(con):
    return dict(macroprocesses=[dict(id=i,name=n) for i,n in con.execute('SELECT id,name FROM macroprocesses ORDER BY name')],processes=[dict(id=i,macroprocess_id=m,name=n) for i,m,n in con.execute('SELECT id,macroprocess_id,name FROM processes ORDER BY name')])


def check_processes(con,data):
    macros=data.get('macroprocess_ids',[]);processes=data.get('process_ids',[])
    if not isinstance(macros,list) or not isinstance(processes,list) or not macros or not processes:
        raise ValueError('Selecione pelo menos um macroprocesso e um processo')
    if len(set(macros))!=len(macros) or len(set(processes))!=len(processes):
        raise ValueError('Seleções de processos duplicadas')
    catalog=process_catalog(con)
    if any(not any(m['id']==mid for m in catalog['macroprocesses']) for mid in macros) or any(not any(p['id']==pid and p['macroprocess_id'] in macros for p in catalog['processes']) for pid in processes):
        raise ValueError('Selecione processos vinculados aos macroprocessos escolhidos')
    if any(not any(p['id'] in processes and p['macroprocess_id']==mid for p in catalog['processes']) for mid in macros):
        raise ValueError('Selecione um processo por macroprocesso')
    data['macroprocesses']=' · '.join(next(m['name'] for m in catalog['macroprocesses'] if m['id']==mid) for mid in macros)
    data['processes']='\n'.join(next(m['name'] for m in catalog['macroprocesses'] if m['id']==p['macroprocess_id'])+' › '+p['name'] for pid in processes for p in catalog['processes'] if p['id']==pid)


def catalog(con, table):
    return [dict(json.loads(payload), id=i) for i,payload in con.execute(f'SELECT id,payload FROM {table}')]


def norm_names():
    with connection() as con:
        return [n for (n,) in con.execute('SELECT name FROM norms ORDER BY id')]


def role_set(value):
    return value if isinstance(value, list) else ['leader', 'auditor'] if value == 'leader' else [value] if value else []


def check_selections(con, data, required=False):
    if not required and 'lead_id' not in data:
        return  # Existing legacy snapshots remain readable.
    auditors = catalog(con, 'auditors')
    requirements = catalog(con, 'requirements')
    leader = next((a for a in auditors if a['id'] == data.get('lead_id')), None)
    if not leader or not all('leader' in role_set(leader['qualifications'].get(n)) for n in data['norms']):
        raise ValueError('Selecione um líder habilitado em todas as normas')
    team_ids = data.get('team_ids', [])
    if not isinstance(team_ids, list) or len(set(team_ids)) != len(team_ids):
        raise ValueError('Equipe inválida')
    for aid in team_ids:
        auditor = next((a for a in auditors if a['id'] == aid), None)
        if aid == leader['id'] or not auditor or not any('auditor' in role_set(auditor['qualifications'].get(n)) for n in data['norms']):
            raise ValueError('Auditor não habilitado para as normas selecionadas')
    observer_ids = data.get('observer_ids', [])
    if not isinstance(observer_ids, list) or len(set(observer_ids)) != len(observer_ids):
        raise ValueError('Observadores inválidos')
    for aid in observer_ids:
        observer = next((a for a in auditors if a['id'] == aid), None)
        if aid == leader['id'] or aid in team_ids or not observer or not any('observer' in role_set(observer['qualifications'].get(n)) for n in data['norms']):
            raise ValueError('Selecione observadores habilitados, sem repetir integrantes da equipe')
    data['observers'] = ', '.join(next(a['name'] for a in auditors if a['id'] == aid) for aid in observer_ids)
    ids = data.get('requirement_ids', [])
    selected = [r for r in requirements if r['id'] in ids]
    if not ids or len(selected) != len(ids) or any(r['norm'] not in data['norms'] for r in selected):
        raise ValueError('Selecione requisitos cadastrados nas normas escolhidas')
    if any(not any(r['norm'] == n for r in selected) for n in data['norms']):
        raise ValueError('Selecione um requisito para cada norma')
    data['lead'] = leader['name']
    data['team'] = ', '.join(next(a['name'] for a in auditors if a['id'] == aid) for aid in team_ids)
    data['requirements'] = '\n'.join(f"{r['norm']} · {r['code']} — {r.get('title', '')}" for r in selected)


def validate_periods(periods):
    from datetime import date, time
    import re
    if not isinstance(periods, list) or not periods:
        raise ValueError('Informe pelo menos um dia e horário')
    rows = []
    for period in periods:
        date.fromisoformat(period.get('date', ''))
        start, end = period.get('start', ''), period.get('end', '')
        if not re.fullmatch(r'([01]\d|2[0-3]):[0-5]\d', start) or not re.fullmatch(r'([01]\d|2[0-3]):[0-5]\d', end) or time.fromisoformat(start) >= time.fromisoformat(end):
            raise ValueError('Informe horários válidos; o fim deve ser posterior ao início')
        rows.append(dict(date=period['date'], start=start, end=end))
    rows.sort(key=lambda p: (p['date'], p['start']))
    for previous, current in zip(rows, rows[1:]):
        if previous['date'] == current['date'] and previous['end'] > current['start']:
            raise ValueError('Há períodos sobrepostos no mesmo dia')
    return rows


def normalize_finding(con, data, audit):
    result=dict(audit_id=data['audit_id'],kind=data['kind'],area=audit['area'],description='' if data['kind']=='Conformidade' else data['description'].strip(),auditor_name=(audit.get('plan') or audit).get('lead',''))
    if data['kind'] in ['Conformidade','Não conformidade']:
        ids=data['requirement_ids']
        if len(set(ids))!=len(ids):
            raise ValueError('Requisitos duplicados')
        requirements=catalog(con,'requirements')
        selected=[next((r for r in requirements if r['id']==rid),None) for rid in ids]
        plan=audit.get('plan',{})
        allowed=plan.get('requirement_ids')
        if any(not r or (r['id'] not in allowed if allowed else r['norm'] not in plan.get('norms',[])) for r in selected):
            raise ValueError('Selecione requisitos que pertencem ao plano da auditoria')
        result.update(requirement_ids=ids,requirements='\n'.join(f"{r['norm']} · {r['code']} — {r.get('title','')}" for r in selected),evidence=data['evidence'].strip())
        if data['kind']=='Não conformidade':
            result['pac']=data['pac'].strip()
    return result


def validate(data, finding=False):
    if finding:
        required = ['audit_id','kind']+([] if data.get('kind')=='Conformidade' else ['description'])
    else:
        required = ['audit_type','year','month','area','lead','norms','requirements']
    for key in required:
        if not data.get(key) or isinstance(data[key],str) and not data[key].strip():
            raise ValueError('Preencha o campo: '+key)
    if finding:
        if data.get('kind')!='Conformidade' and not isinstance(data.get('description'),str):
            raise ValueError('Informe a descrição da constatação')
        if data['kind'] not in ['Conformidade','Não conformidade','Observação','Oportunidade de melhoria']:
            raise ValueError('Classificação inválida')
        if data['kind'] in ['Conformidade','Não conformidade']:
            if not isinstance(data.get('requirement_ids'),list) or not data['requirement_ids']:
                raise ValueError('Selecione requisitos do plano')
            if not isinstance(data.get('evidence'),str) or not data['evidence'].strip():
                raise ValueError('Informe a evidência encontrada' if data['kind']=='Conformidade' else 'Informe as evidências objetivas')
        if data['kind']=='Não conformidade' and (not isinstance(data.get('pac'),str) or not data['pac'].strip()):
            raise ValueError('Informe o número do PAC da não conformidade')
    else:
        if data['audit_type'] not in ['I', 'F']:
            raise ValueError('Tipo de auditoria inválido')
        if not 1 <= int(data['month']) <= 12 or not 2000 <= int(data['year']) <= 2100:
            raise ValueError('Ano ou mês inválido')
        if not isinstance(data['norms'], list) or not data['norms'] or any(n not in norm_names() for n in data['norms']):
            raise ValueError('Selecione normas válidas')
        if data.get('status') not in ['Programada', 'Plano em rascunho', 'Planejada', 'Em execução', 'Concluída']:
            raise ValueError('Situação inválida')
        plan = data.get('plan')
        if plan:
            from datetime import date
            if 'periods' in plan:
                plan['periods'] = validate_periods(plan['periods'])
                plan['date'] = plan['periods'][0]['date']
            else:
                date.fromisoformat(plan.get('date', ''))
            if not plan.get('lead', '').strip() or not plan.get('requirements', '').strip():
                raise ValueError('Informe o auditor líder e os requisitos do plano')
            if not plan.get('norms') or any(n not in norm_names() for n in plan['norms']):
                raise ValueError('Selecione as normas do plano')
        if data['status'] != 'Programada' and not plan:
            raise ValueError('Gere o plano primeiro')
        if (data.get('report') or data['status'] in ['Planejada', 'Em execução', 'Concluída']) and not (plan and plan.get('confirmed') is True):
            raise ValueError('Confirme o plano antes de gerar o relatório')
        if data['audit_type']=='I' and isinstance(data.get('report'),dict):
            data['report'].pop('conclusion',None)
        if data['audit_type']=='F' and data['status'] == 'Concluída' and not data.get('report', {}).get('conclusion', '').strip():
            raise ValueError('Informe a conclusão do relatório')

class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT / 'static'), **kwargs)

    def send_json(self, data, status=200, token=None):
        body = json.dumps(data, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Cache-Control', 'no-store')
        if token is not None:
            secure = '; Secure' if self.headers.get('X-Forwarded-Proto') == 'https' else ''
            self.send_header('Set-Cookie', f'nuclaud_session={token}; Path=/; HttpOnly; SameSite=Lax; Max-Age={access.SESSION_SECONDS if token else 0}{secure}')
        self.end_headers()
        self.wfile.write(body)

    def user(self):
        with connection() as con:
            return access.current_user(con, self.headers.get('Cookie'))

    def do_GET(self):
        if self.path == '/healthz':
            return self.send_json({'ok':True})
        if self.path == '/api/session':
            with connection() as con:
                user = access.current_user(con, self.headers.get('Cookie'))
                needs_setup = con.execute('SELECT count(*) FROM users').fetchone()[0] == 0
            return self.send_json(dict(user=user, needs_setup=needs_setup))
        if self.path.startswith('/api/') and not self.user():
            return self.send_json({'error':'Entre com seu nome de rede e senha'}, 401)
        if self.path == '/api/data':
            user = self.user()
            with connection() as con:
                audits = [dict(json.loads(p), id=i) for i, p in con.execute('SELECT id,payload FROM audits ORDER BY id DESC')]
                findings = [dict(json.loads(p), id=i, audit_id=a) for i, a, p in con.execute('SELECT id,audit_id,payload FROM findings ORDER BY id')]
                areas = [dict(id=i, name=n) for i,n in con.execute('SELECT id,name FROM areas ORDER BY name')]
                auditors = catalog(con, 'auditors')
                requirements = catalog(con, 'requirements')
                norms = [n for (n,) in con.execute('SELECT name FROM norms ORDER BY id')]
                processes = process_catalog(con)
                users = access.users(con) if user['role'] == 'admin' else []
            if user['role'] != 'admin':
                audits = [a for a in audits if access.can_manage(user,a)]
                findings = [f for f in findings if any(a['id']==f['audit_id'] for a in audits)]
            return self.send_json(dict(**processes,current_user=user,users=users,audits=audits, findings=findings, areas=areas, auditors=auditors, requirements=requirements, norms=norms, requirement_sources=requirement_sources()))
        if self.path.startswith('/api/'):
            return self.send_json({'error': 'Não encontrado'}, 404)
        super().do_GET()

    def do_POST(self):
        self.save()

    def do_PUT(self):
        self.save()

    def save(self):
        try:
            size = int(self.headers.get('Content-Length', 0))
            if not 0 < size <= 1000000:
                raise ValueError('Tamanho de solicitação inválido')
            data = json.loads(self.rfile.read(size))
            if not isinstance(data, dict):
                raise ValueError('Dados inválidos')
            origin = self.headers.get('Origin')
            if origin and urlsplit(origin).netloc != self.headers.get('Host'):
                raise PermissionError('Origem de solicitação não permitida')
            if not self.headers.get('Content-Type', '').startswith('application/json'):
                raise ValueError('Use Content-Type application/json')
            if self.path == '/api/setup' and self.command == 'POST':
                setup_token = os.environ.get('NUCLAUD_SETUP_TOKEN')
                local = self.client_address[0] in ['127.0.0.1', '::1'] and not self.headers.get('X-Forwarded-For')
                if not local and (not setup_token or not secrets.compare_digest(str(data.get('setup_token', '')), setup_token)):
                    raise PermissionError('Informe a chave de instalação configurada no servidor')
                with connection() as con:
                    con.execute('BEGIN IMMEDIATE')
                    if con.execute('SELECT 1 FROM users').fetchone():
                        raise PermissionError('Administrador inicial já cadastrado')
                    user, _ = access.create_user(con, dict(data, role='admin'), catalog(con,'auditors'), password=data.get('password',''))
                    token = access.session(con,user['id'])
                return self.send_json(dict(user=user),201,token)
            if self.path == '/api/login' and self.command == 'POST':
                with connection() as con:
                    row = con.execute('SELECT id,password_hash FROM users WHERE username=?', (str(data.get('username','')).strip().casefold(),)).fetchone()
                    if not row or not access.password_matches(data.get('password',''),row[1]):
                        return self.send_json({'error':'Nome de rede ou senha inválidos'},401)
                    token = access.session(con,row[0])
                return self.send_json({'ok':True},200,token)
            if self.path == '/api/logout' and self.command == 'POST':
                token = access.cookie_token(self.headers.get('Cookie'))
                with connection() as con:
                    con.execute('DELETE FROM sessions WHERE token_hash=?', (hashlib.sha256(token.encode()).hexdigest(),))
                return self.send_json({'ok':True},200,'')
            user = self.user()
            if not user:
                return self.send_json({'error':'Sessão ausente ou expirada'},401)
            if self.path == '/api/password' and self.command == 'POST':
                with connection() as con:
                    stored=con.execute('SELECT password_hash FROM users WHERE id=?',(user['id'],)).fetchone()[0]
                    if not access.password_matches(data.get('current_password',''),stored):
                        return self.send_json({'error':'Senha atual inválida'},403)
                    con.execute('UPDATE users SET password_hash=? WHERE id=?',(access.password_hash(data.get('new_password','')),user['id']))
                    con.execute('DELETE FROM sessions WHERE user_id=?',(user['id'],))
                    token=access.session(con,user['id'])
                return self.send_json({'ok':True},200,token)
            if user['role'] != 'admin' and not (self.path.startswith('/api/audits/') and self.command == 'PUT' or self.path == '/api/findings' and self.command == 'POST' or self.path.startswith('/api/findings/') and self.command=='PUT'):
                raise PermissionError('Esta operação exige perfil Administrador')
            if self.path == '/api/users' and self.command == 'POST':
                with connection() as con:
                    con.execute('BEGIN IMMEDIATE')
                    new_user, temporary = access.create_user(con,data,catalog(con,'auditors'))
                return self.send_json(dict(user=new_user,temporary_password=temporary),201)
            if self.path in ['/api/macroprocesses','/api/processes'] and self.command=='POST':
                name=str(data.get('name','')).strip()
                if not name or len(name)>150:
                    raise ValueError('Informe um nome de até 150 caracteres')
                with connection() as con:
                    con.execute('BEGIN IMMEDIATE')
                    if self.path=='/api/macroprocesses':
                        if any(n.casefold()==name.casefold() for (n,) in con.execute('SELECT name FROM macroprocesses')):
                            raise ValueError('Macroprocesso já cadastrado')
                        rid=con.execute('INSERT INTO macroprocesses(name) VALUES (?)',(name,)).lastrowid
                        result=dict(id=rid,name=name)
                    else:
                        mid=int(data.get('macroprocess_id') or 0)
                        if not con.execute('SELECT 1 FROM macroprocesses WHERE id=?',(mid,)).fetchone():
                            raise ValueError('Selecione um macroprocesso cadastrado')
                        if any(n.casefold()==name.casefold() for (n,) in con.execute('SELECT name FROM processes WHERE macroprocess_id=?',(mid,))):
                            raise ValueError('Processo já cadastrado neste macroprocesso')
                        rid=con.execute('INSERT INTO processes(macroprocess_id,name) VALUES (?,?)',(mid,name)).lastrowid
                        result=dict(id=rid,name=name,macroprocess_id=mid)
                return self.send_json(result,201)
            if self.path.startswith('/api/requirements/') and self.command == 'PUT':
                title = str(data.get('title','')).strip()
                if not title:
                    raise ValueError('Informe o título do requisito')
                with connection() as con:
                    rid = int(self.path.split('/')[-1])
                    row = con.execute('SELECT payload FROM requirements WHERE id=?',(rid,)).fetchone()
                    if not row:
                        return self.send_json({'error':'Requisito não encontrado'},404)
                    requirement = json.loads(row[0]);requirement['title']=title
                    con.execute('UPDATE requirements SET payload=? WHERE id=?',(json.dumps(requirement),rid))
                return self.send_json({'ok':True})
            if self.path == '/api/norms' and self.command == 'POST':
                name = str(data.get('name', '')).strip()
                if not name or len(name) > 150:
                    raise ValueError('Informe o nome da norma (até 150 caracteres)')
                with connection() as con:
                    con.execute('BEGIN IMMEDIATE')
                    if any(n.casefold() == name.casefold() for (n,) in con.execute('SELECT name FROM norms')):
                        raise ValueError('Norma já cadastrada')
                    row = con.execute('INSERT INTO norms(name) VALUES (?)', (name,)).lastrowid
                return self.send_json(dict(id=row, name=name), 201)
            if self.path in ['/api/auditors', '/api/requirements'] and self.command == 'POST':
                table = self.path.split('/')[-1]
                if table == 'auditors':
                    name = str(data.get('name', '')).strip()
                    qualifications = data.get('qualifications', {})
                    if not name or not isinstance(qualifications, dict) or not qualifications or any(n not in norm_names() or not isinstance(r, list) or not r or len(set(r)) != len(r) or any(role not in ['leader', 'auditor', 'observer'] for role in r) for n,r in qualifications.items()):
                        raise ValueError('Informe nome e habilitações válidas')
                    data = dict(name=name, qualifications=qualifications)
                else:
                    if data.get('norm') not in norm_names() or not str(data.get('code', '')).strip() or not str(data.get('title', '')).strip():
                        raise ValueError('Informe norma, código e título')
                    data = dict(norm=data['norm'], code=data['code'].strip(), title=data['title'].strip())
                with connection() as con:
                    con.execute('BEGIN IMMEDIATE')
                    entries = catalog(con, table)
                    if any((e['name'].casefold() == data['name'].casefold()) if table == 'auditors' else (e['norm'] == data['norm'] and e['code'].casefold() == data['code'].casefold()) for e in entries):
                        raise ValueError('Cadastro duplicado')
                    row = con.execute(f'INSERT INTO {table}(payload) VALUES (?)', (json.dumps(data),)).lastrowid
                return self.send_json(dict(data, id=row), 201)
            if self.path == '/api/areas' and self.command == 'POST':
                name = str(data.get('name', '')).strip()
                if not name or len(name) > 150:
                    raise ValueError('Informe o nome da área (até 150 caracteres)')
                with connection() as con:
                    con.execute('BEGIN IMMEDIATE')
                    names = [n for (n,) in con.execute('SELECT name FROM areas')]
                    if any(n.casefold() == name.casefold() for n in names):
                        raise ValueError('Essa área já está cadastrada')
                    row = con.execute('INSERT INTO areas(name) VALUES (?)', (name,)).lastrowid
                return self.send_json({'id': row, 'name': name}, 201)
            if self.path == '/api/findings' and self.command == 'POST' or self.path.startswith('/api/findings/') and self.command=='PUT':
                validate(data, True)
                with connection() as con:
                    con.execute('BEGIN IMMEDIATE')
                    finding_id=None
                    if self.command=='PUT':
                        finding_id=int(self.path.split('/')[-1])
                        old=con.execute('SELECT audit_id FROM findings WHERE id=?',(finding_id,)).fetchone()
                        if not old:
                            return self.send_json({'error':'Constatação não encontrada'},404)
                        if int(data['audit_id'])!=old[0]:
                            raise ValueError('Não é permitido transferir a constatação para outra auditoria')
                    audit = con.execute('SELECT payload FROM audits WHERE id=?', (int(data['audit_id']),)).fetchone()
                    if audit and not access.can_manage(user,json.loads(audit[0])):
                        raise PermissionError('Você não é o líder desta auditoria')
                    if not audit or not json.loads(audit[0]).get('plan', {}).get('confirmed'):
                        raise ValueError('Confirme o plano antes de registrar o relatório')
                    if json.loads(audit[0]).get('status')=='Concluída':
                        raise ValueError('O relatório está concluído; as constatações estão preservadas')
                    data=normalize_finding(con,data,dict(json.loads(audit[0]),id=int(data['audit_id'])))
                    if finding_id is not None:
                        con.execute('UPDATE findings SET payload=? WHERE id=?',(json.dumps(data),finding_id))
                        row=finding_id
                    else:
                        row = con.execute('INSERT INTO findings(audit_id,payload) VALUES (?,?)', (int(data['audit_id']), json.dumps(data))).lastrowid
                return self.send_json({'id': row}, 200 if finding_id is not None else 201)
            if self.path == '/api/audits' and self.command == 'POST':
                validate(data)
                if data.get('status')=='Concluída':
                    raise ValueError('Registre e classifique os requisitos antes de concluir o relatório')
                with connection() as con:
                    con.execute('BEGIN IMMEDIATE')
                    check_area(con, data)
                    check_processes(con,data)
                    check_selections(con, data, required=True)
                    if data.get('plan'):
                        check_selections(con, data['plan'], required=True)
                    data['code'] = next_code(con, data['audit_type'], data['year'])
                    data.pop('title', None)
                    row = con.execute('INSERT INTO audits(payload) VALUES (?)', (json.dumps(data),)).lastrowid
                return self.send_json({'id': row, 'code': data['code']}, 201)
            if self.path.startswith('/api/audits/') and self.command == 'PUT':
                validate(data)
                with connection() as con:
                    con.execute('BEGIN IMMEDIATE')
                    old = con.execute('SELECT payload FROM audits WHERE id=?', (int(self.path.split('/')[-1]),)).fetchone()
                    if old:
                        previous = json.loads(old[0])
                        access.check_update(user,previous,data)
                        if data['audit_type'] != previous['audit_type'] or int(data['year']) != int(previous['year']):
                            raise ValueError('Tipo e ano não podem mudar após a geração do código')
                        data['code'] = previous['code']
                        data.pop('title', None)
                        check_area(con, data)
                        if previous.get('macroprocess_ids')!=data.get('macroprocess_ids') or previous.get('process_ids')!=data.get('process_ids'):
                            check_processes(con,data)
                        if any(previous.get(k) != data.get(k) for k in ['norms','lead_id','team_ids','observer_ids','requirement_ids','lead','team','requirements']):
                            check_selections(con, data, required=True)
                        if data.get('plan') and previous.get('plan') != data['plan']:
                            check_selections(con, data['plan'], required=True)
                        has_findings = con.execute('SELECT 1 FROM findings WHERE audit_id=?', (int(self.path.split('/')[-1]),)).fetchone()
                        if (previous.get('report') or has_findings) and previous.get('plan') != data.get('plan'):
                            raise ValueError('O relatório já foi iniciado; preserve o plano confirmado')
                    if data.get('status')=='Concluída':
                        required_ids=set(data.get('plan',{}).get('requirement_ids',[]))
                        covered=set()
                        for (payload,) in con.execute('SELECT payload FROM findings WHERE audit_id=?',(int(self.path.split('/')[-1]),)):
                            finding=json.loads(payload)
                            if finding.get('kind') in ['Conformidade','Não conformidade']:
                                covered.update(finding.get('requirement_ids',[]))
                        missing=required_ids-covered
                        if not required_ids or missing:
                            labels=[f"{r['norm']} · {r['code']} — {r.get('title','')}" for r in catalog(con,'requirements') if r['id'] in missing]
                            raise ValueError('Classifique todos os requisitos em conformidade ou não conformidade antes de concluir o relatório. Pendentes: '+('; '.join(labels) or str(len(missing))))
                        data.setdefault('report',{})
                    cur = con.execute('UPDATE audits SET payload=? WHERE id=?', (json.dumps(data), int(self.path.split('/')[-1])))
                    if not cur.rowcount:
                        return self.send_json({'error': 'Auditoria não encontrada'}, 404)
                return self.send_json({'ok': True})
            return self.send_json({'error': 'Não encontrado'}, 404)
        except PermissionError as exc:
            self.send_json({'error':str(exc)},403)
        except (ValueError, TypeError, sqlite3.IntegrityError) as exc:
            self.send_json({'error': str(exc)}, 400)

if __name__ == '__main__':
    connection().close()
    ThreadingHTTPServer(('0.0.0.0', int(os.environ.get('PORT', '8000'))), Handler).serve_forever()
