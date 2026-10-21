"""Supplier program UI without internal catalogs and numbering reset after deletion."""
from pathlib import Path
from playwright.sync_api import sync_playwright
html=Path('/tmp/nuclaud-preview/NUCLAUD-v12.html').read_text()
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=browser.new_page();page.set_default_timeout(8000)
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('http://127.0.0.1:8123/**',lambda r:r.fulfill(content_type='text/html',body=html))
    page.goto('http://127.0.0.1:8123/')
    page.evaluate("async()=>{await api('auditors','POST',{name:'Ana',qualifications:{'ISO 9001:2015':['leader']}});await load();}")
    page.locator('#year').select_option('2027')
    page.locator('#new-audit').click()
    page.locator('[name=audit_type]').select_option('F')
    assert not page.locator('#internal-program-fields').is_visible()
    page.locator('[name=audit_type]').select_option('I')
    assert page.locator('#internal-program-fields').is_visible()
    page.locator('[name=audit_type]').select_option('F')
    page.locator('[name=supplier_name]').fill('Fornecedor Teste')
    page.locator('[name=supplier_cnpj]').fill('12.345.678/0001-95')
    page.locator('[name=supplier_address]').fill('Rua Teste, 100')
    page.locator('[name=supply_scope]').fill('Fornecimento de componentes')
    page.locator('[data-multi=norms] summary').click()
    page.get_by_label('ISO 9001:2015',exact=True).check()
    page.locator('[name=lead_id]').check()
    page.locator('[name=requirement_ids]').first.check()
    page.get_by_role('button',name='Salvar programação',exact=True).click()
    page.locator('#modal').wait_for(state='hidden')
    assert 'AUDI-F-001-2027' in page.locator('#app').inner_text()
    record=page.evaluate('data.audits[0]')
    assert record['area_names']==[] and record['macroprocess_ids']==[] and record['process_ids']==[]
    page.get_by_role('button',name='Gerar plano',exact=True).click()
    assert 'Macroprocessos' not in page.locator('#fields').inner_text()
    assert 'Processos:' not in page.locator('#fields').inner_text()
    assert 'Fornecedor Teste' in page.locator('#fields').inner_text()
    page.locator('[name=period_date]').fill('2027-03-10')
    page.locator('[name=period_start]').fill('08:00')
    page.locator('[name=period_end]').fill('12:00')
    page.locator('[name=scope]').fill('Avaliar a qualificação do fornecedor')
    page.get_by_role('button',name='Salvar plano',exact=True).click()
    page.get_by_role('button',name='◫ Calendário',exact=True).click()
    page.get_by_role('button',name='Gerar relatório',exact=True).click()
    page.get_by_role('button',name='Editar relatório',exact=True).click()
    page.get_by_label('Qualificado por um ano',exact=True).check()
    page.get_by_label('Observações da conclusão',exact=True).fill('Fornecedor qualificado')
    page.get_by_label('Número do RQF',exact=True).fill('RQF-2027-456')
    page.locator('#modal').get_by_role('button',name='Salvar relatório',exact=True).click()
    page.locator('#modal').wait_for(state='hidden')
    assert 'RQF-2027-456' in page.locator('#app').inner_text()
    page.evaluate('window.print=()=>{}')
    with page.expect_download() as download:
        page.get_by_role('button',name='Gerar PDF do relatório',exact=True).click()
    download.value.save_as('/tmp/nuclaud-preview/supplier-download.pdf')
    assert 'RQF-2027-456' in page.locator('#print-document').inner_text()
    assert 'Fornecedor qualificado' in page.locator('#print-document').inner_text()
    assert 'Qualificado por um ano' in page.locator('#print-document').inner_text()
    page.get_by_role('button',name='Editar relatório',exact=True).click()
    assert page.locator('[name=qualification_result]:checked').input_value()=='one_year'
    assert page.locator('[name=qualification_result]').count()==3
    page.get_by_label('Não qualificado',exact=True).check()
    assert page.locator('[name=qualification_result]:checked').count()==1
    page.get_by_role('button',name='Cancelar',exact=True).click()
    assert not page.locator('#print-document dt').filter(has_text='Macroprocessos').count()
    # Delete the sole record, then repeatedly create/delete. Old counters must not affect allocation.
    result=page.evaluate('''async()=>{
      const original=data.audits[0];await api('audits/'+original.id,'DELETE',{});
      const codes=[];
      for(let i=0;i<4;i++){
        const created=await api('audits','POST',{...original,status:'Programada',plan:undefined,report:undefined});
        const state=await api('data');codes.push(state.audits.find(a=>a.id===created.id).code);
        await api('audits/'+created.id,'DELETE',{});
      }
      const one=await api('audits','POST',{...original,status:'Programada',plan:undefined,report:undefined});
      const two=await api('audits','POST',{...original,status:'Programada',plan:undefined,report:undefined});
      const state=await api('data');return {codes,remaining:state.audits.map(a=>a.code)};
    }''')
    assert result['codes']==['AUDI-F-001-2027']*4,result
    assert result['remaining']==['AUDI-F-001-2027','AUDI-F-002-2027'],result
    assert not errors,errors
    browser.close()
print('PASS: supplier form without areas/processes, RQF persistence/PDF, numbering reset and collision-free subsequent codes.')
