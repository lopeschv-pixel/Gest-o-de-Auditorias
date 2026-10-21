"""Regression of classification forms and generated A4 report/plan PDFs."""
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from playwright.sync_api import sync_playwright

root=Path('/tmp/nuclaud-preview')
html=(root/'NUCLAUD-v12.html').read_text()
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=browser.new_page()
    page.set_default_timeout(8000)
    errors=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('http://127.0.0.1:8123/**',lambda r:r.fulfill(content_type='text/html',body=html))
    page.goto('http://127.0.0.1:8123/')
    fixture=page.evaluate('''async()=>{
        await api('areas','POST',{name:'Qualidade'});
        const m=await api('macroprocesses','POST',{name:'Gestão'});
        const process=await api('processes','POST',{name:'Qualidade',macroprocess_id:m.id});
        const ana=await api('auditors','POST',{name:'Ana Líder',qualifications:{'ISO 9001:2015':['leader','auditor']}});
        const bruno=await api('auditors','POST',{name:'Bruno Auditor',qualifications:{'ISO 9001:2015':['auditor']}});
        const state=await api('data');
        const requirements=state.requirements.filter(r=>r.norm==='ISO 9001:2015'&&['8.4','7.2'].includes(r.code));
        const plan={lead:'Ana Líder',lead_id:ana.id,team:'Bruno Auditor',team_ids:[bruno.id],norms:['ISO 9001:2015'],requirement_ids:requirements.map(r=>r.id),requirements:requirements.map(r=>r.norm+' · '+r.code+' — '+r.title).join('\\n'),confirmed:true,date:'2027-03-10',periods:[{date:'2027-03-10',start:'08:00',end:'12:00'},{date:'2027-03-11',start:'13:00',end:'17:00'}],manager_email:'gestor@example.com',location:'Sala de reuniões',scope:'Avaliar processos e requisitos previstos.'};
        window.reportFixture={audit_type:'I',year:2027,month:3,area:'Qualidade',area_names:['Qualidade'],macroprocess_ids:[m.id],process_ids:[process.id],macroprocesses:'Gestão',processes:'Gestão › Qualidade',lead:'Ana Líder',lead_id:ana.id,team:'Bruno Auditor',team_ids:[bruno.id],norms:plan.norms,requirement_ids:plan.requirement_ids,requirements:plan.requirements,plan,status:'Planejada'};
        const result=await api('audits','POST',reportFixture);await load();return result;
    }''')
    page.locator('#year').select_option('2027')
    page.get_by_role('button',name='◫ Calendário',exact=True).click()
    page.get_by_role('button',name='Gerar relatório',exact=True).click()
    assert 'Ana Líder' in page.locator('#app').inner_text()
    assert 'Bruno Auditor' in page.locator('#app').inner_text()
    page.get_by_role('button',name='Concluir relatório',exact=True).click()
    assert 'Faltam 2 requisito(s)' in page.locator('#report-validation').inner_text()
    assert '7.2' in page.locator('#report-validation').inner_text()
    assert '8.4' in page.locator('#report-validation').inner_text()
    page.locator('#save-report').click()
    alerts=[]
    page.on('dialog',lambda dialog:(alerts.append(dialog.message),dialog.accept()))
    page.get_by_role('button',name='Concluir relatório',exact=True).click()
    assert 'Classifique todos' in page.locator('#report-validation').inner_text()
    assert page.locator('#complete-report').count()==1
    for i,kind in enumerate(['Conformidade','Não conformidade','Observação','Oportunidade de melhoria']):
        page.get_by_role('button',name='＋ Registrar constatação',exact=True).click()
        assert page.locator('#modal-title').inner_text()=='Constatação'
        assert page.locator('[name=kind] option').all_text_contents()==['Conformidade','Não conformidade','Observação','Oportunidade de melhoria']
        page.locator('[name=kind]').select_option(kind)
        if kind in ['Conformidade','Não conformidade']:
            page.locator('[data-multi=requirement_ids] summary').click()
            page.locator('#fields [name=requirement_ids]').first.check()
            if kind=='Conformidade':page.locator('#fields [name=requirement_ids]').last.check()
            page.locator('[data-multi=requirement_ids] summary').click()
            assert page.locator('#fields textarea').count()==(1 if kind=='Conformidade' else 2)
            label='Evidência encontrada' if kind=='Conformidade' else 'Evidências objetivas'
            page.get_by_label(label+' *',exact=True).fill('Registro verificado. '+('Evidência detalhada. '*120 if kind=='Não conformidade' else '')+' FINAL DA EVIDÊNCIA')
            if kind=='Não conformidade':
                page.get_by_label('Número do PAC *',exact=True).fill('PAC-2027-001')
                assert page.get_by_label('Descrição da não conformidade *',exact=True).count()==1
        else:
            assert page.locator('#fields textarea').count()==1
            assert page.locator('#fields [name=evidence]').count()==0
            assert page.locator('#fields [name=requirement_ids]').count()==0
            assert page.locator('#fields [name=pac]').count()==0
            assert page.get_by_label(kind+' *',exact=True).count()==1
        description=(kind+' registrada. ')* (110 if kind=='Não conformidade' else 1)
        if kind!='Conformidade':page.locator('#fields [name=description]').fill(description)
        else:assert page.locator('#fields [name=description]').count()==0
        page.get_by_role('button',name='Salvar registro',exact=True).click()
        page.locator('#modal').wait_for(state='hidden')
        assert page.locator('#app .finding-card').count()==i+1
    page.get_by_role('button',name='Editar relatório',exact=True).click()
    assert page.locator('[name=conclusion]').count()==0
    page.locator('[name=participants]').fill('Gestor da área auditada')
    page.locator('#modal').get_by_role('button',name='Salvar relatório',exact=True).click()
    page.locator('#modal').wait_for(state='hidden')
    page.get_by_role('button',name='Editar relatório',exact=True).click()
    page.locator('[name=participants]').fill('Participantes preservados durante edição')
    page.locator('#fields [data-edit-finding]').first.click()
    assert page.locator('#modal-title').inner_text()=='Editar constatação'
    assert page.locator('[name=evidence]').input_value().startswith('Registro verificado.')
    page.locator('[data-multi=requirement_ids] summary').click()
    page.locator('[name=requirement_ids]').last.uncheck()
    page.locator('.multi-done').click()
    page.locator('[name=evidence]').fill('Evidência corrigida e salva')
    page.get_by_role('button',name='Salvar registro',exact=True).click()
    page.locator('#modal').wait_for(state='hidden')
    assert page.locator('#app .finding-card').count()==4
    assert 'Participantes preservados durante edição' in page.locator('#app').inner_text()
    assert 'Evidência corrigida e salva' in page.locator('#app').inner_text()
    page.get_by_role('button',name='Concluir relatório',exact=True).click()
    assert 'Faltam 1 requisito(s)' in page.locator('#report-validation').inner_text()
    assert '8.4' in page.locator('#report-validation').inner_text()
    page.locator('#app [data-edit-finding]').first.click()
    page.locator('[data-multi=requirement_ids] summary').click()
    page.locator('[name=requirement_ids]').last.check()
    page.locator('.multi-done').click()
    page.get_by_role('button',name='Salvar registro',exact=True).click()
    page.locator('#modal').wait_for(state='hidden')
    header=page.locator('#app .finding-heading-label').first
    assert 'Constatação 01' in header.inner_text() and 'Conformidade' in header.inner_text()
    page.get_by_role('button',name='Concluir relatório',exact=True).click()
    assert page.locator('#complete-report').count()==0
    assert page.locator('#app [data-edit-finding]').count()==4  # administrator can correct completed reports
    page.evaluate('window.print=()=>{}')
    page.get_by_role('button',name='Gerar PDF do relatório',exact=True).click()
    print_text=page.locator('#print-document').inner_text()
    assert 'Assinatura do auditor líder' in print_text
    assert 'Descrição da conformidade' not in print_text
    assert 'Plano de auditoria' not in print_text
    assert 'Conclusão' not in print_text
    assert 'Ana Líder' in print_text and 'Bruno Auditor' in print_text
    assert 'Gestão › Qualidade' in print_text
    page.emulate_media(media='print')
    page.pdf(path=str(root/'report-internal.pdf'),prefer_css_page_size=True,print_background=True)
    page.emulate_media(media='screen')
    supplier=page.evaluate("async()=>{const result=await api('audits','POST',{...reportFixture,audit_type:'F'});selected={id:result.id,tab:'report'};await load();return result;}")
    page.get_by_role('button',name='Editar relatório',exact=True).click()
    page.locator('#modal').get_by_role('button',name='Salvar relatório',exact=True).click()
    page.locator('#modal').wait_for(state='hidden')
    page.get_by_role('button',name='Editar relatório',exact=True).click()
    page.get_by_label('Conclusão da auditoria de fornecedores',exact=True).fill('Fornecedor avaliado conforme critérios previstos.')
    page.locator('#modal').get_by_role('button',name='Salvar relatório',exact=True).click()
    page.locator('#modal').wait_for(state='hidden')
    page.get_by_role('button',name='Gerar PDF do relatório',exact=True).click()
    page.emulate_media(media='print')
    page.pdf(path=str(root/'report-supplier.pdf'),prefer_css_page_size=True,print_background=True)
    page.emulate_media(media='screen')
    page.evaluate('selected.tab="plan";render()')
    page.get_by_role('button',name='Gerar PDF do plano',exact=True).click()
    page.emulate_media(media='print')
    page.pdf(path=str(root/'plan.pdf'),prefer_css_page_size=True,print_background=True)
    assert not errors,errors
    browser.close()

for name in ['report-internal','report-supplier','plan']:
    pdf=root/(name+'.pdf');text_path=root/(name+'.txt');bbox_path=root/(name+'.xhtml')
    subprocess.run(['pdftotext','-layout',str(pdf),str(text_path)],check=True)
    subprocess.run(['pdftotext','-bbox-layout',str(pdf),str(bbox_path)],check=True)
    text=text_path.read_text();tree=ET.parse(bbox_path)
    pages=tree.findall('.//{http://www.w3.org/1999/xhtml}page')
    assert pages
    for page in pages:
        width,height=float(page.get('width')),float(page.get('height'))
        assert 594<width<596 and 841<height<843
        for word in page.findall('.//{http://www.w3.org/1999/xhtml}word'):
            assert 15<float(word.get('xMin'))<float(word.get('xMax'))<width-15,(name,word.text)
            assert 15<float(word.get('yMin'))<float(word.get('yMax'))<height-15,(name,word.text)
    assert 'Administração do sistema' not in text and 'Perfil de teste' not in text
    assert 'Ana Líder' in text and 'Bruno Auditor' in text
    if name=='report-internal':
        assert len(pages)>=2
        assert 'FINAL DA EVIDÊNCIA' in text
        assert 'PAC-2027-001' in text
        assert 'Conclusão' not in text
        assert 'Plano de auditoria' not in text
    elif name=='report-supplier':assert 'Conclusão da auditoria de fornecedores' in text
    else:assert 'Plano de auditoria' in text and 'Constatação 01' not in text
    print('PASS:',name,'—',len(pages),'página(s) A4, textos completos e margens verificadas.')
subprocess.run(['pdftoppm','-f','1','-singlefile','-scale-to','1400','-png',str(root/'report-internal.pdf'),str(root/'report-first-page')],check=True)
