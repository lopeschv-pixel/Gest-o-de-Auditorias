"""Browser regression: registered auditors/requirements flow in standalone preview."""
from pathlib import Path
from playwright.sync_api import sync_playwright
html=Path('/tmp/nuclaud-preview/NUCLAUD-v12.html').read_text()
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    for blocked in [False,True]:
        context=browser.new_context()
        page=context.new_page()
        page.set_default_timeout(5000)
        errors=[]
        page.on('pageerror',lambda e:errors.append(str(e)))
        if blocked:
            page.add_init_script("Object.defineProperty(window,'localStorage',{get(){throw new DOMException('Blocked','SecurityError')}})")
        page.route('http://127.0.0.1:8123/**',lambda r:r.fulfill(content_type='text/html',body=html))
        page.goto('http://127.0.0.1:8123/')
        assert page.evaluate('JSON.parse(localStorage.getItem("nuclaud-preview-v1")).requirements.length')==339 if not blocked else True
        page.get_by_role('button',name='☷ Cadastrar normas e requisitos',exact=True).click()
        assert page.locator('#norm-register').is_visible()
        assert not page.locator('#modal').is_visible()
        page.get_by_role('button',name='＋ Cadastrar norma',exact=True).click()
        page.locator('#norm-name').fill('Norma personalizada')
        page.get_by_role('button',name='Salvar norma',exact=True).click()
        page.locator('#app').get_by_text('Norma personalizada',exact=True).wait_for()
        page.locator('.catalog-row').filter(has=page.get_by_text('ISO 9001:2015 · 8.4 — Controle de fornecimento externo',exact=True)).get_by_role('button',name='Editar título',exact=True).click()
        assert page.locator('[name=norm]').is_disabled()
        page.locator('[name=title]').fill('Título local ajustado')
        page.get_by_role('button',name='Salvar requisito',exact=True).click()
        page.get_by_text('ISO 9001:2015 · 8.4 — Título local ajustado',exact=True).wait_for()
        for name,role in [('Ana','leader'),('Bruno','auditor'),('Carla','observer')]:
            page.get_by_role('button',name='♙ Cadastrar auditores',exact=True).click()
            page.get_by_role('button',name='＋ Cadastrar auditor',exact=True).click()
            page.locator('[name=name]').fill(name)
            assert page.locator('fieldset').filter(has_text='Secretaria Naval').count()==1
            assert page.locator('fieldset').filter(has_text='Norma personalizada').count()==1
            page.locator(f'[name=qualification_0][value={role}]').check()
            if name=='Ana': page.locator('[name=qualification_0][value=auditor]').check()
            page.locator('[name=qualification_2][value='+('leader' if name=='Ana' else role)+']').check()
            page.get_by_role('button',name='Salvar auditor',exact=True).click()
            page.locator('#app').get_by_text(name,exact=True).wait_for()
        for norm,code in [('ISO 9001:2015','98.4'),('ISO 14001','96.1')]:
            page.get_by_role('button',name='☷ Cadastrar normas e requisitos',exact=True).click()
            page.get_by_role('button',name='＋ Cadastrar requisito',exact=True).click()
            page.locator('[name=norm]').select_option(norm)
            page.locator('[name=code]').fill(code)
            page.locator('[name=title]').fill('Título '+code)
            assert page.locator('[name=description]').count()==0
            page.get_by_role('button',name='Salvar requisito',exact=True).click()
            page.locator('#app').get_by_text(norm+' · '+code+' — Título '+code,exact=True).wait_for()
        for area in ['Engenharia','Qualidade']:
            page.get_by_role('button',name='＋ Cadastrar áreas',exact=True).click()
            page.get_by_role('button',name='＋ Cadastrar área',exact=True).click()
            page.locator('#area-name').fill(area)
            page.get_by_role('button',name='Salvar área',exact=True).click()
            page.locator('#app').get_by_text(area,exact=True).wait_for()
            page.locator('#area-register').get_by_text('Área salva com sucesso.',exact=True).wait_for()
            assert not page.locator('#modal').is_visible()
        page.locator('#area-name').fill('Qualidade')
        page.get_by_role('button',name='Salvar área',exact=True).click()
        page.locator('#area-register .form-error').wait_for()
        page.get_by_role('button',name='▦ Macroprocessos e processos',exact=True).click()
        for macro in ['Produção','Gestão']:
            page.locator('#macro-register [name=name]').fill(macro)
            page.get_by_role('button',name='Salvar macroprocesso',exact=True).click()
            page.locator('#macro-register .form-message').get_by_text('Cadastro salvo com sucesso.',exact=True).wait_for()
        for macro,process in [('Produção','Soldagem'),('Gestão','Qualidade')]:
            page.locator('#process-register [name=macroprocess_id]').select_option(label=macro)
            page.locator('#process-register [name=name]').fill(process)
            page.locator('#process-register').get_by_label('ISO 9001:2015',exact=True).check()
            page.locator('#process-register').get_by_label('ISO 14001',exact=True).check()
            page.get_by_role('button',name='Salvar processo',exact=True).click()
            page.locator('#process-register .form-message').get_by_text('Cadastro salvo com sucesso.',exact=True).wait_for()
        page.get_by_role('button',name='＋ Programar auditoria').click()
        assert page.locator('#modal-title').inner_text()=='Programar auditoria'
        assert page.locator('#inline-area-name').count()==0
        assert page.locator('[data-multi=area_names]').count()==1
        assert page.locator('[data-multi=norms]').count()==1
        assert not page.locator('[data-multi=norms]').evaluate('(el)=>el.open')
        page.locator('[data-multi=area_names] summary').click()
        page.get_by_label('Engenharia',exact=True).check()
        page.get_by_label('Qualidade',exact=True).check()
        page.locator('[data-multi=area_names] summary').click()
        page.locator('[data-multi=norms] summary').click()
        assert page.locator('#fields').get_by_label('Secretaria Naval',exact=True).count()==1
        assert page.locator('#fields').get_by_label('Norma personalizada',exact=True).count()==1
        page.locator('#fields').get_by_label('ISO 9001:2015',exact=True).check()
        assert page.locator('[name=lead_id][type=radio]').count()==1
        assert page.locator('[name=requirement_ids]').count()>20
        assert page.evaluate("[...document.querySelectorAll('[name=requirement_ids]')].every(x=>/ · \\d+\\.\\d+(?:\\.\\d+)? — /.test(x.closest('label').textContent))")
        page.locator('#fields').get_by_label('ISO 14001',exact=True).check()
        assert page.locator('[name=requirement_ids]').count()>40
        page.locator('[data-multi=norms] summary').click()
        page.locator('[data-multi=macroprocess_ids] summary').click()
        page.get_by_label('Produção',exact=True).check()
        page.get_by_label('Gestão',exact=True).check()
        page.locator('[data-multi=macroprocess_ids] summary').click()
        page.locator('[data-multi=process_ids] summary').click()
        page.get_by_label('Produção › Soldagem',exact=True).check()
        page.get_by_label('Gestão › Qualidade',exact=True).check()
        page.locator('[data-multi=process_ids] summary').click()
        page.locator('[name=lead_id]').check()
        page.get_by_label('Bruno',exact=True).check()
        page.get_by_label('Carla',exact=True).check()
        page.get_by_label('ISO 9001:2015 · 98.4 — Título 98.4',exact=True).check()
        page.get_by_role('button',name='Salvar programação',exact=True).click()
        page.get_by_text('Selecione pelo menos um requisito para cada norma.',exact=True).wait_for()
        page.get_by_label('ISO 14001 · 96.1 — Título 96.1',exact=True).check()
        page.get_by_role('button',name='Salvar programação',exact=True).click()
        page.get_by_role('button',name='Editar programação').wait_for()
        page.get_by_role('button',name='Editar programação').click()
        assert page.locator('[name=area_names]:checked').count()==2
        assert page.locator('[name=norms]:checked').count()==2
        assert page.locator('[name=macroprocess_ids]:checked').count()==2
        assert page.locator('[name=process_ids]:checked').count()==2
        page.get_by_role('button',name='Salvar programação',exact=True).click()
        page.get_by_role('button',name='Gerar plano',exact=True).click()
        assert page.locator('[name=lead_id]:checked').count()==1
        assert page.locator('[name=team_ids]:checked').count()==1
        assert page.locator('[name=observer_ids]:checked').count()==1
        assert page.locator('[name=requirement_ids]:checked').count()==2
        assert page.locator('[name=agenda]').count()==0
        assert page.get_by_label('Local',exact=True).count()==1
        assert page.get_by_label('E-mail do gestor',exact=True).count()==1
        assert page.locator('[name=scope]').count()==0
        page.locator('[name=manager_name]').fill('Maria Gestora')
        page.locator('[name=manager_email]').fill('gestor@example.com')
        page.locator('[name=period_date]').fill('2027-03-10')
        page.locator('[name=period_start]').fill('08:00')
        page.locator('[name=period_end]').fill('12:00')
        page.get_by_role('button',name='＋ Adicionar dia / período').click()
        page.locator('[name=period_date]').nth(1).fill('2027-03-11')
        page.locator('[name=period_start]').nth(1).fill('13:00')
        page.locator('[name=period_end]').nth(1).fill('17:00')
        page.get_by_role('button',name='Salvar plano').click()
        page.get_by_role('button',name='Gerar PDF do plano').wait_for()
        assert '10/03/2027 08:00–12:00' in page.locator('#app').inner_text()
        assert '11/03/2027 13:00–17:00' in page.locator('#app').inner_text()
        page.evaluate('window.print=()=>{}')
        page.get_by_role('button',name='Gerar PDF do plano',exact=True).click()
        assert all(value in page.locator('#print-document').inner_text() for value in ['11/03/2027','13:00','17:00'])
        assert 'gestor@example.com' in page.locator('#print-document').inner_text()
        assert page.get_by_role('button',name='Preparar e-mail',exact=True).count()==0
        assert 'Rascunho' not in page.locator('#app').inner_text()
        assert 'Engenharia · Qualidade' in page.locator('#app').inner_text()
        assert 'Bruno' in page.locator('#app').inner_text()
        assert 'Carla' in page.locator('#app').inner_text()
        assert page.locator('nav [data-view=reports]').count()==0
        page.get_by_role('button',name='♙ Planejamento de auditores',exact=True).click()
        assert '10/03/2027 08:00–12:00' in page.locator('#app').inner_text()
        assert '2 pontos' in page.locator('#app').inner_text()
        page.get_by_role('button',name='↳ Ranqueamento de auditores',exact=True).click()
        rows=page.locator('#app tbody tr')
        assert rows.nth(0).locator('td').last.inner_text()=='2'
        assert 'Ana' in rows.nth(0).inner_text()
        assert 'Bruno' in rows.nth(1).inner_text()
        assert 'Carla' in rows.nth(2).inner_text()
        page.locator('#rank-order').select_option('asc')
        assert 'Carla' in page.locator('#app tbody tr').first.inner_text()
        page.get_by_role('button',name='◫ Calendário',exact=True).click()
        page.get_by_role('button',name='Gerar relatório',exact=True).click()
        page.get_by_role('button',name='Editar relatório',exact=True).wait_for()
        assert page.evaluate('data.requirements.filter(r=>r.title).length')==341
        assert page.evaluate('data.requirements.length')==341
        assert page.evaluate('data.requirement_sources.length')==4
        page.get_by_role('button',name='♙ Cadastrar usuários',exact=True).click()
        page.get_by_role('button',name='＋ Cadastrar usuário',exact=True).click()
        page.locator('[name=username]').fill('ana.rede')
        page.locator('[name=full_name]').fill('Ana da Rede')
        page.locator('[name=email]').fill('ana@example.com')
        page.locator('[name=auditor_id]').select_option(label='Ana')
        page.get_by_role('button',name='Salvar usuário',exact=True).click()
        page.locator('#app').get_by_text('ana.rede',exact=True).wait_for()
        assert page.locator('#admin-menu').is_visible()
        page.locator('#demo-profile').select_option(label='Ana da Rede — Usuário')
        page.get_by_role('button',name='Gerar relatório',exact=True).wait_for()
        assert not page.locator('#admin-menu').is_visible()
        assert not page.locator('#new-audit').is_visible()
        denied=page.evaluate("async()=>{try{await api('areas','POST',{name:'Não permitido'});return false;}catch(e){return e.message.includes('Administrador');}}")
        assert denied
        page.get_by_role('button',name='Gerar relatório',exact=True).click()
        page.get_by_role('button',name='Editar relatório',exact=True).click()
        page.locator('[name=participants]').fill('Ana')
        assert page.locator('[name=conclusion]').count()==0
        page.locator('[name=participants]').fill('Ana e Bruno, participantes')
        page.locator('#modal').get_by_role('button',name='Salvar relatório',exact=True).click()
        assert 'Ana e Bruno, participantes' in page.locator('#app').inner_text()
        page.locator('#demo-profile').select_option(label='Administrador da demonstração — Administrador')
        page.get_by_role('button',name='♙ Cadastrar usuários',exact=True).click()
        page.locator('#app').get_by_text('ana.rede',exact=True).wait_for()
        if blocked:
            page.locator('#storage-warning').wait_for()
        else:
            page.reload()
            page.get_by_role('button',name='♙ Cadastrar auditores',exact=True).click()
            page.locator('#app').get_by_text('Ana',exact=True).wait_for()
            page.get_by_role('button',name='☷ Cadastrar normas e requisitos',exact=True).click()
            page.locator('#app').get_by_text('ISO 14001 · 96.1 — Título 96.1',exact=True).wait_for()
            assert page.evaluate('data.requirements.filter(r=>r.title).length')==341
        assert page.evaluate('data.requirements.length')==341
        assert not errors,errors
        print('PASS: cadastros, habilitações por norma, filtros, programação, plano e persistência; armazenamento bloqueado:',blocked)
        context.close()
    browser.close()
