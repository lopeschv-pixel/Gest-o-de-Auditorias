"""Process norm-specific mandatory requirements, editing and automatic audit selection."""
import re
from pathlib import Path
from playwright.sync_api import sync_playwright
html=Path('/tmp/nuclaud-preview/NUCLAUD-v12.html').read_text()
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=browser.new_page();page.set_default_timeout(8000);errors=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('http://127.0.0.1:8123/**',lambda r:r.fulfill(content_type='text/html',body=html))
    page.goto('http://127.0.0.1:8123/')
    ids=page.evaluate('''async()=>{
      await api('areas','POST',{name:'Qualidade'});await api('macroprocesses','POST',{name:'Gestão'});
      await api('auditors','POST',{name:'Ana',qualifications:{'ISO 9001:2015':['leader'],'ISO 14001':['leader']}});
      await load();return Object.fromEntries(['ISO 9001:2015','ISO 14001'].map(n=>[n,data.requirements.find(r=>r.norm===n&&r.code==='4.1').id]));
    }''')
    page.get_by_role('button',name='▦ Macroprocessos e processos',exact=True).click()
    page.locator('#process-register [name=macroprocess_id]').select_option(label='Gestão')
    page.locator('#process-register [name=name]').fill('Gestão da qualidade')
    for norm,rid in ids.items():
        page.locator('#process-register').get_by_label(norm,exact=True).check()
        group=page.locator(f'#process-register [data-process-norm="{norm}"]')
        assert group.is_visible()
        group.locator('summary').click()
        group.locator(f'[value="{rid}"]').check()
        group.get_by_role('button',name='Concluir seleção').click()
    page.get_by_role('button',name='Salvar processo',exact=True).click()
    page.locator('#process-register .form-message').get_by_text('Cadastro salvo com sucesso.',exact=True).wait_for()
    process=page.evaluate('data.processes[0]')
    assert set(process['norms'])==set(ids)
    assert set(process['mandatory_requirement_ids'])==set(ids.values())
    page.get_by_role('button',name='Editar processo',exact=True).click()
    assert page.locator('#fields [name=norms]:checked').count()==2
    assert page.locator('#fields [name=mandatory_requirement_ids]:checked').count()==2
    page.locator('#fields [name=name]').fill('Gestão da qualidade revisada')
    page.locator('#modal').get_by_role('button',name='Salvar processo',exact=True).click()
    page.locator('#modal').wait_for(state='hidden')
    assert page.evaluate('data.processes[0].id')==process['id']
    page.get_by_role('button',name='Histórico',exact=True).click()
    assert 'Gestão da qualidade revisada' in page.locator('#fields').inner_text()
    page.get_by_role('button',name='Fechar',exact=True).last.click()
    page.locator('#new-audit').click()
    for norm in ids:
        page.locator('[data-multi=norms] summary').click() if not page.locator('[data-multi=norms]').evaluate('(el)=>el.open') else None
        page.locator('#fields').get_by_label(norm,exact=True).check()
    page.locator('[data-multi=area_names] summary').click()
    page.locator('#fields').get_by_label('Qualidade',exact=True).check()
    page.locator('[data-multi=macroprocess_ids] summary').click()
    page.locator('#fields').get_by_label('Gestão',exact=True).check()
    page.locator('[data-multi=process_ids] summary').click()
    page.locator(f'[name=process_ids][value="{process["id"]}"]').check()
    for rid in ids.values():
        required=page.locator(f'[name=requirement_ids][value="{rid}"]')
        assert required.is_checked()
        required.click()
        assert required.is_checked()
    extra=page.evaluate("data.requirements.find(r=>r.norm==='ISO 9001:2015'&&r.code==='4.2').id")
    extra_box=page.locator(f'[name=requirement_ids][value="{extra}"]')
    extra_box.check()
    extra_box.uncheck()
    assert not extra_box.is_checked()
    extra_box.check()
    page.locator('[name=lead_id]').check()
    page.get_by_role('button',name='Salvar programação',exact=True).click()
    page.locator('#modal').wait_for(state='hidden')
    assert set(page.evaluate('data.audits[0].requirement_ids'))==set(ids.values())|{extra}
    page.get_by_role('button',name='Gerar plano',exact=True).click()
    for rid in ids.values():
        checkbox=page.locator(f'[name=requirement_ids][value="{rid}"]')
        assert checkbox.is_checked()
        checkbox.click()
        assert checkbox.is_checked()
    assert not errors,errors
    browser.close()
print('PASS: per-norm mandatory requirements, persistence/editing/history and automatic mandatory audit selection.')
