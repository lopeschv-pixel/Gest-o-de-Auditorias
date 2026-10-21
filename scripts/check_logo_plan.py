"""Logo upload/persistence, direct plan emission and completed calendar labeling."""
import base64
from pathlib import Path
from playwright.sync_api import sync_playwright
html=Path('/tmp/nuclaud-preview/NUCLAUD-v12.html').read_text()
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=browser.new_page();page.set_default_timeout(8000);errors=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('http://127.0.0.1:8123/**',lambda r:r.fulfill(content_type='text/html',body=html))
    page.goto('http://127.0.0.1:8123/')
    image=page.evaluate("()=>{const c=document.createElement('canvas');c.width=120;c.height=40;const ctx=c.getContext('2d');ctx.fillStyle='#174b80';ctx.fillRect(0,0,120,40);ctx.fillStyle='white';ctx.fillText('TESTE',20,25);return c.toDataURL('image/png');}")
    page.get_by_role('button',name='◈ Identidade visual',exact=True).click()
    page.locator('#logo-file').set_input_files({'name':'logo-teste.png','mimeType':'image/png','buffer':base64.b64decode(image.split(',')[1])})
    page.locator('#logo-form .form-message').get_by_text('Prévia pronta. Clique em Salvar logotipo.',exact=True).wait_for()
    page.get_by_role('button',name='Salvar logotipo',exact=True).click()
    page.locator('#logo-form .form-message').get_by_text('Logotipo salvo. Ele será incluído nos próximos PDFs.',exact=True).wait_for()
    saved=page.evaluate('data.settings.logo')
    page.reload()
    page.get_by_role('button',name='◈ Identidade visual',exact=True).click()
    assert page.locator('#logo-preview img').get_attribute('src')==saved
    page.evaluate('''async()=>{
      await api('areas','POST',{name:'Qualidade'});const m=await api('macroprocesses','POST',{name:'Gestão'});
      const process=await api('processes','POST',{name:'Qualidade',macroprocess_id:m.id,norms:['ISO 9001:2015']});
      const leader=await api('auditors','POST',{name:'Ana',qualifications:{'ISO 9001:2015':['leader']}});
      const req=data.requirements.find(r=>r.norm==='ISO 9001:2015'&&r.code==='4.1');
      await api('audits','POST',{audit_type:'I',year:2027,month:3,area:'Qualidade',area_names:['Qualidade'],macroprocess_ids:[m.id],process_ids:[process.id],norms:['ISO 9001:2015'],lead_id:leader.id,lead:'Ana',team_ids:[],requirement_ids:[req.id],requirements:'4.1',status:'Programada'});await load();
    }''')
    page.locator('#year').select_option('2027')
    page.get_by_role('button',name='◫ Calendário',exact=True).click()
    page.get_by_role('button',name='Gerar plano',exact=True).click()
    page.locator('[name=period_date]').fill('2027-03-10');page.locator('[name=period_start]').fill('08:00');page.locator('[name=period_end]').fill('12:00')
    page.get_by_role('button',name='Salvar plano',exact=True).click()
    page.get_by_role('button',name='Gerar PDF do plano',exact=True).wait_for()
    assert page.get_by_role('button',name='Confirmar plano',exact=True).count()==0
    assert page.get_by_role('button',name='Preparar e-mail',exact=True).count()==0
    assert 'Rascunho' not in page.locator('#app').inner_text()
    with page.expect_download() as download:page.get_by_role('button',name='Gerar PDF do plano',exact=True).click()
    download.value.save_as('/tmp/nuclaud-preview/logo-plan.pdf')
    assert page.locator('#print-document .doc-logo').get_attribute('src')==saved
    page.evaluate("async()=>{const a=data.audits[0];await api('findings','POST',{audit_id:a.id,kind:'Conformidade',requirement_ids:a.plan.requirement_ids,evidence:'Registro'});await api('audits/'+a.id,'PUT',{...a,status:'Concluída',report:{}});await load();}")
    page.get_by_role('button',name='◫ Calendário',exact=True).click()
    card=page.locator('.audit-card.audit-color-completed')
    assert 'Realizada' in card.inner_text()
    assert 'Confirmado' not in card.inner_text() and 'Concluída' not in card.inner_text()
    assert not errors,errors
    browser.close()
print('PASS: logo file upload/persistence and PDF branding, no draft/confirmation/email controls and gray calendar labeled Realizada.')
