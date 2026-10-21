"""Check real server bootstrap/login, admin/user access and leader-only plan flow."""
import json
from pathlib import Path
import sys
import tempfile
import threading
from http.server import ThreadingHTTPServer
sys.path.insert(0,str(Path(__file__).resolve().parent.parent))
import server
from playwright.sync_api import sync_playwright

with tempfile.TemporaryDirectory() as directory:
    server.DB=Path(directory)/'browser.db'
    http=ThreadingHTTPServer(('127.0.0.1',0),server.Handler)
    threading.Thread(target=http.serve_forever,daemon=True).start()
    try:
        with sync_playwright() as p:
            browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
            page=browser.new_page()
            errors=[]
            page.on('pageerror',lambda e:errors.append(str(e)))
            page.route('https://fonts.googleapis.com/**',lambda r:r.abort())
            page.goto(f'http://127.0.0.1:{http.server_port}/')
            page.locator('[name=username]').fill('admin')
            page.locator('[name=full_name]').fill('Administrador de teste')
            page.locator('[name=email]').fill('admin@example.com')
            page.locator('[name=password]').fill('Example-admin-password')
            page.get_by_role('button',name='Criar administrador',exact=True).click()
            page.locator('#admin-menu').wait_for()
            assert not page.locator('#demo-profile').count()
            result=page.evaluate('''async()=>{
                await api('areas','POST',{name:'Compras'});
                const macro=await api('macroprocesses','POST',{name:'Gestão'});
                const process=await api('processes','POST',{name:'Compras',macroprocess_id:macro.id});
                const auditor=await api('auditors','POST',{name:'Ana',qualifications:{'ISO 9001:2015':['leader','auditor']}});
                const state=await api('data');
                const r=state.requirements.find(r=>r.norm==='ISO 9001:2015'&&r.code==='8.4');
                await api('audits','POST',{audit_type:'I',macroprocess_ids:[macro.id],process_ids:[process.id],year:new Date().getFullYear(),month:3,area:'Compras',lead:'Ana',lead_id:auditor.id,norms:['ISO 9001:2015'],team_ids:[],requirement_ids:[r.id],requirements:'8.4',status:'Programada'});
                return await api('users','POST',{username:'ana.rede',full_name:'Ana da Rede',email:'ana@example.com',role:'user',auditor_id:auditor.id});
            }''')
            password=result['temporary_password']
            page.get_by_role('button',name='Sair',exact=True).click()
            page.locator('[name=username]').fill('ana.rede')
            page.locator('[name=password]').fill(password)
            page.get_by_role('button',name='Entrar',exact=True).click()
            page.get_by_role('button',name='Gerar plano',exact=True).wait_for()
            assert not page.locator('#admin-menu').is_visible()
            assert not page.locator('#new-audit').is_visible()
            page.get_by_role('button',name='Gerar plano',exact=True).click()
            assert page.locator('[name=lead_id][type=radio]').count()==1
            page.locator('[name=period_date]').fill('2027-03-10')
            page.locator('[name=period_start]').fill('08:00')
            page.locator('[name=period_end]').fill('12:00')
            page.get_by_role('button',name='Salvar plano para conferência',exact=True).click()
            page.get_by_role('button',name='Confirmar plano',exact=True).click()
            page.get_by_role('button',name='Gerar PDF do plano',exact=True).wait_for()
            page.get_by_role('button',name='◫ Calendário',exact=True).click()
            page.get_by_role('button',name='Gerar relatório',exact=True).click()
            page.get_by_role('button',name='Editar relatório',exact=True).click()
            page.locator('[name=participants]').fill('Ana')
            assert page.locator('[name=conclusion]').count()==0
            page.locator('[name=participants]').fill('Ana, participante de teste')
            page.get_by_role('button',name='Salvar registro',exact=True).click()
            page.get_by_text('Ana, participante de teste',exact=True).wait_for()
            assert not errors,errors
            browser.close()
            print('PASS: real server first-admin setup, login, role restrictions, leader-owned plan and report.')
    finally:
        http.shutdown();http.server_close()
