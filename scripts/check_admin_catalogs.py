"""Admin editing/deletion and process requirement alignment/numbering in the browser."""
from pathlib import Path
from playwright.sync_api import sync_playwright
html=Path('/tmp/nuclaud-preview/NUCLAUD-v12.html').read_text()
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=browser.new_page(viewport={'width':1280,'height':900});page.set_default_timeout(8000)
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('http://127.0.0.1:8123/**',lambda r:r.fulfill(content_type='text/html',body=html))
    page.goto('http://127.0.0.1:8123/')
    ids=page.evaluate('''async()=>{
      for(const code of ['4.2.1','4.4.1.1'])if(!data.requirements.some(r=>r.norm==='ANSN 1.16'&&r.code===code))await api('requirements','POST',{norm:'ANSN 1.16',code,title:'Requisito de teste'});
      await load();return Object.fromEntries(data.requirements.filter(r=>r.norm==='ANSN 1.16').map(r=>[r.code,r.id]));
    }''')
    page.get_by_role('button',name='▦ Macroprocessos e processos',exact=True).click()
    page.locator('#macro-register [name=name]').fill('Macro teste')
    page.get_by_role('button',name='Salvar macroprocesso',exact=True).click()
    page.get_by_role('button',name='Editar macroprocesso',exact=True).click()
    page.locator('#fields [name=name]').fill('Macro revisado')
    page.locator('#modal').get_by_role('button',name='Salvar macroprocesso',exact=True).click()
    page.locator('#modal').wait_for(state='hidden')
    page.locator('#process-register [name=macroprocess_id]').select_option(label='Macro revisado')
    page.locator('#process-register [name=name]').fill('Processo teste')
    norm=page.locator('#process-register [name=norms][value="ANSN 1.16"]');norm.check()
    group=page.locator('#process-register [data-process-norm="ANSN 1.16"]')
    group.locator('summary').click()
    for code in ['4.2','4.2.1','4.4','4.4.1']:
        assert group.locator(f'[name=mandatory_requirement_ids][value="{ids[code]}"]').count()==1,code
    assert group.locator(f'[value="{ids["4.4.1.1"]}"]').count()==0
    codes=group.locator('[name=mandatory_requirement_ids]').evaluate_all('(xs)=>xs.map(x=>data.requirements.find(r=>r.id===Number(x.value)).code)')
    assert page.evaluate('(codes)=>codes.every((code,i)=>!i||codes[i-1].localeCompare(code,undefined,{numeric:true})<=0)',codes)
    required=group.locator(f'[value="{ids["4.4.1"]}"]')
    for width in [1280,520]:
        page.set_viewport_size({'width':width,'height':900})
        left=norm.bounding_box();box=required.bounding_box()
        assert abs(left['x']-box['x'])<3,(left,box)
        assert 12<=box['width']<=20,box
    page.set_viewport_size({'width':1280,'height':900})
    required.check();group.get_by_role('button',name='Concluir seleção',exact=True).click()
    page.get_by_role('button',name='Salvar processo',exact=True).click()
    page.get_by_role('button',name='Editar processo',exact=True).click()
    page.locator('#fields [name=name]').fill('Processo revisado')
    page.locator('#modal').get_by_role('button',name='Salvar processo',exact=True).click()
    page.locator('#modal').wait_for(state='hidden')
    assert page.get_by_role('button',name='Excluir processo',exact=True).count()==1
    page.once('dialog',lambda d:d.dismiss())
    page.get_by_role('button',name='Excluir processo',exact=True).click()
    assert page.get_by_role('button',name='Editar processo',exact=True).count()==1
    page.once('dialog',lambda d:d.accept())
    page.get_by_role('button',name='Excluir processo',exact=True).click()
    assert page.get_by_role('button',name='Editar processo',exact=True).count()==0
    page.once('dialog',lambda d:d.accept())
    page.get_by_role('button',name='Excluir macroprocesso',exact=True).click()
    assert page.get_by_role('button',name='Editar macroprocesso',exact=True).count()==0
    page.get_by_role('button',name='♙ Cadastrar usuários',exact=True).click()
    page.get_by_role('button',name='＋ Cadastrar usuário',exact=True).click()
    page.locator('[name=username]').fill('admin.segundo')
    page.locator('[name=full_name]').fill('Segundo administrador')
    page.locator('[name=email]').fill('segundo@example.com')
    page.locator('[name=role]').select_option('admin')
    page.get_by_role('button',name='Salvar usuário',exact=True).click()
    row=page.locator('tr').filter(has_text='admin.segundo')
    row.get_by_role('button',name='Editar usuário',exact=True).click()
    assert page.locator('[name=username]').input_value()=='admin.segundo'
    page.locator('[name=full_name]').fill('Nome revisado')
    page.get_by_role('button',name='Salvar usuário',exact=True).click()
    row=page.locator('tr').filter(has_text='admin.segundo')
    assert 'Nome revisado' in row.inner_text()
    page.once('dialog',lambda d:d.accept())
    row.get_by_role('button',name='Excluir usuário',exact=True).click()
    assert page.locator('tr').filter(has_text='admin.segundo').count()==0
    assert not errors,errors
    browser.close()
print('PASS: left aligned checkboxes on desktop/mobile, numeric levels 4.4/4.4.1, and admin edit/delete for all three catalogs.')
