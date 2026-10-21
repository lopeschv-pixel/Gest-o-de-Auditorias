"""Local authentication and audit authorization for the server application."""
import hashlib
import hmac
import re
import secrets
import time
from http.cookies import SimpleCookie

SESSION_SECONDS = 8 * 60 * 60
ITERATIONS = 600_000


def schema(con):
    con.execute('CREATE TABLE IF NOT EXISTS users (id INTEGER PRIMARY KEY, username TEXT UNIQUE NOT NULL, full_name TEXT NOT NULL, email TEXT NOT NULL, role TEXT NOT NULL CHECK(role IN (\'admin\',\'user\')), auditor_id INTEGER REFERENCES auditors(id), password_hash TEXT NOT NULL)')
    con.execute('CREATE TABLE IF NOT EXISTS sessions (token_hash TEXT PRIMARY KEY, user_id INTEGER NOT NULL REFERENCES users(id), expires INTEGER NOT NULL)')


def password_hash(password):
    if not isinstance(password, str) or not 10 <= len(password) <= 1024:
        raise ValueError('A senha deve ter de 10 a 1024 caracteres')
    salt = secrets.token_hex(16)
    value = hashlib.pbkdf2_hmac('sha256', password.encode(), bytes.fromhex(salt), ITERATIONS).hex()
    return salt + '$' + value


def password_matches(password, stored):
    if not isinstance(password, str) or len(password) > 1024:
        return False
    salt, expected = stored.split('$')
    actual = hashlib.pbkdf2_hmac('sha256', password.encode(), bytes.fromhex(salt), ITERATIONS).hex()
    return hmac.compare_digest(actual, expected)


def public_user(row):
    return dict(zip(['id', 'username', 'full_name', 'email', 'role', 'auditor_id'], row[:6]))


def users(con):
    return [public_user(row) for row in con.execute('SELECT id,username,full_name,email,role,auditor_id FROM users ORDER BY full_name')]


def validate_user(data, auditors):
    if any(not isinstance(data.get(key),str) for key in ['username','full_name','email']):
        raise ValueError('Informe nome de rede, nome completo e e-mail válidos')
    username = str(data.get('username', '')).strip().casefold()
    full_name = str(data.get('full_name', '')).strip()
    email = str(data.get('email', '')).strip()
    role = data.get('role')
    auditor_id = data.get('auditor_id') or None
    if not re.fullmatch(r'[\w.@\\-]{2,100}', username) or not full_name or len(full_name) > 200 or not re.fullmatch(r'[^@\s]+@[^@\s]+\.[^@\s]+', email):
        raise ValueError('Informe nome de rede, nome completo e e-mail válidos')
    if role not in ['admin', 'user']:
        raise ValueError('Perfil inválido')
    if auditor_id is not None:
        auditor_id = int(auditor_id)
    if role == 'user':
        auditor = next((a for a in auditors if a['id'] == auditor_id), None)
        if not auditor or not any('leader' in (r if isinstance(r, list) else ['leader'] if r == 'leader' else []) for r in auditor['qualifications'].values()):
            raise ValueError('Vincule o usuário a um auditor com habilitação de líder')
    return dict(username=username, full_name=full_name, email=email, role=role, auditor_id=auditor_id)


def create_user(con, data, auditors, password=None):
    user = validate_user(data, auditors)
    if con.execute('SELECT 1 FROM users WHERE username=?', (user['username'],)).fetchone():
        raise ValueError('Nome de rede já cadastrado')
    temporary = secrets.token_urlsafe(15) if password is None else None
    stored = password_hash(temporary if temporary else password)
    uid = con.execute('INSERT INTO users(username,full_name,email,role,auditor_id,password_hash) VALUES (?,?,?,?,?,?)', (user['username'], user['full_name'], user['email'], user['role'], user['auditor_id'], stored)).lastrowid
    return dict(user, id=uid), temporary


def session(con, user_id):
    token = secrets.token_urlsafe(32)
    con.execute('DELETE FROM sessions WHERE expires<=?', (int(time.time()),))
    con.execute('INSERT INTO sessions(token_hash,user_id,expires) VALUES (?,?,?)', (hashlib.sha256(token.encode()).hexdigest(), user_id, int(time.time()) + SESSION_SECONDS))
    return token


def cookie_token(header):
    cookie = SimpleCookie()
    try:
        cookie.load(header or '')
        return cookie['nuclaud_session'].value if 'nuclaud_session' in cookie else ''
    except Exception:
        return ''


def current_user(con, header):
    token = cookie_token(header)
    if not token:
        return None
    row = con.execute('SELECT u.id,u.username,u.full_name,u.email,u.role,u.auditor_id FROM sessions s JOIN users u ON u.id=s.user_id WHERE s.token_hash=? AND s.expires>?', (hashlib.sha256(token.encode()).hexdigest(), int(time.time()))).fetchone()
    return public_user(row) if row else None


def can_manage(user, audit):
    return user['role'] == 'admin' or (user.get('auditor_id') is not None and (audit.get('plan') or audit).get('lead_id') == user['auditor_id'])


def check_update(user, previous, updated):
    if user['role'] == 'admin':
        return
    if not can_manage(user, previous):
        raise PermissionError('Você só pode gerar planos e relatórios das auditorias em que é líder')
    allowed = {'plan', 'report', 'status', 'id'}
    if any(previous.get(key) != updated.get(key) for key in set(previous) | set(updated) if key not in allowed):
        raise PermissionError('Somente administradores podem alterar a programação')
    if not updated.get('plan') or updated['plan'].get('lead_id') != user['auditor_id']:
        raise PermissionError('O usuário não pode transferir a liderança da auditoria')
