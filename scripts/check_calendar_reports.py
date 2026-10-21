"""Browser regression: calendar colors/filter, finding deletion and completed-report permissions."""
from pathlib import Path
from playwright.sync_api import sync_playwright
html=Path('/tmp/nuclaud-preview/NUCLAUD-v12.html').read_text()
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=browser.new_page();page.set_default_timeout(8000)
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('http://127.0.0.1:8123/**',lambda r:r.fulfill(content_type='text/html',body=html))
    page.goto('http://127.0.0.1:8123/')
    page.evaluate('''async()=>{
      await api('areas','POST',{name:'Qualidade'});
      const m=await api('macroprocesses','POST',{name:'Gestão'});
      const process=await api('processes','POST',{name:'Qualidade',macroprocess_id:m.id,norms:[...norms]});
      const leader=await api('auditors','POST',{name:'Ana',qualifications:Object.fromEntries(norms.map(n=>[n,['leader','auditor']]))});
      const groups=[['ISO 9001:2015'],['ANSN 1.16','Secretaria Naval'],['ISO 14001','ISO 45001'],['ASME III'],['ISO 9001:2015']];
      window.calendarFixtures=[];
      for(const ns of groups){
        for(const n of ns){const state=await api('data');if(!state.requirements.some(r=>r.norm===n&&r.code==='8.4'))await api('requirements','POST',{norm:n,code:'8.4',title:'Requisito de teste'});}
        const state=await api('data'),rs=ns.map(n=>state.requirements.find(r=>r.norm===n&&r.code==='8.4'));
        const selections={lead_id:leader.id,lead:'Ana',team_ids:[],norms:ns,requirement_ids:rs.map(r=>r.id),requirements:rs.map(r=>r.norm+' · '+r.code).join('\\n')};
        const a={audit_type:'I',year:2027,month:3,area:'Qualidade',area_names:['Qualidade'],macroprocess_ids:[m.id],process_ids:[process.id],...selections,status:'Planejada',plan:{...selections,periods:[{date:'2027-03-10',start:'08:00',end:'12:00'}],date:'2027-03-10',confirmed:true}};
        const result=await api('audits','POST',a);calendarFixtures.push({...a,id:result.id});
      }
      const a=calendarFixtures.at(-1);
      const f=await api('findings','POST',{audit_id:a.id,kind:'Conformidade',requirement_ids:a.plan.requirement_ids,evidence:'Registro analisado'});
      window.completedFinding=f.id;await api('audits/'+a.id,'PUT',{...a,status:'Concluída',report:{participants:'Gestor'}});
      await api('users','POST',{username:'ana.rede',full_name:'Ana',email:'ana@example.com',role:'user',auditor_id:leader.id});
      await load();
    }''')
    page.locator('#year').select_option('2027')
    page.get_by_role('button',name='◫ Calendário',exact=True).click()
    assert page.locator('.audit-card').count()==5
    for color in ['quality','nuclear','sms','asme','completed']:
        assert page.locator('.audit-card.audit-color-'+color).count()==1,color
    page.locator('#calendar-norm summary').click()
    page.locator('#calendar-norm').get_by_label('ISO 45001',exact=True).check()
    assert page.locator('.audit-card').count()==1
    assert page.locator('.audit-card').get_attribute('class').endswith('audit-color-sms')
    page.locator('#calendar-norm').get_by_label('Todas as normas',exact=True).check()
    page.locator('#calendar-norm').get_by_label('ISO 9001:2015',exact=True).check()
    assert page.locator('.audit-card').count()==2
    page.locator('#calendar-norm').get_by_label('Todas as normas',exact=True).check()
    page.locator('#calendar-norm').get_by_label('Secretaria Naval',exact=True).check()
    assert page.locator('.audit-card').count()==1
    page.locator('#calendar-norm').get_by_label('Todas as normas',exact=True).check()
    page.locator('#close-calendar-filter').click()
    assert page.locator('.audit-card').count()==5
    # Readonly completed report for regular leader, including direct API calls.
    page.locator('#demo-profile').select_option(label='Ana — Usuário')
    page.evaluate("selected={id:calendarFixtures.at(-1).id,tab:'report'};render()")
    assert page.locator('#edit-report').count()==0
    assert page.locator('[data-delete-finding]').count()==0
    assert page.locator('[data-edit-finding]').count()==0
    assert page.evaluate("async()=>{try{await api('findings/'+completedFinding,'DELETE',{audit_id:calendarFixtures.at(-1).id});return false;}catch(e){return e.message.includes('administradores');}}")
    # Administrator can edit completed report; cancel then confirm deletion.
    page.locator('#demo-profile').select_option(label='Administrador da demonstração — Administrador')
    page.evaluate("selected={id:calendarFixtures.at(-1).id,tab:'report'};render()")
    page.get_by_role('button',name='Editar relatório',exact=True).click()
    assert page.locator('#fields [data-delete-finding]').count()==1
    page.locator('#fields [name=participants]').fill('Gestor atualizado')
    page.once('dialog',lambda d:d.dismiss())
    page.locator('#fields [data-delete-finding]').click()
    assert page.locator('#modal').is_visible()
    assert page.locator('#fields [data-delete-finding]').count()==1
    page.once('dialog',lambda d:d.accept())
    page.locator('#fields [data-delete-finding]').click()
    page.locator('#modal').wait_for(state='hidden')
    assert page.locator('#app .finding-card').count()==0
    assert 'Gestor atualizado' in page.locator('#app').inner_text()
    page.get_by_role('button',name='Concluir relatório',exact=True).click()
    assert 'Faltam 1 requisito' in page.locator('#report-validation').inner_text()
    page.get_by_role('button',name='◫ Calendário',exact=True).click()
    assert page.locator('.audit-card.audit-color-completed').count()==0
    assert page.locator('.audit-card.audit-color-quality').count()==2
    first_id=page.locator('[data-delete-audit]').first.get_attribute('data-delete-audit')
    page.once('dialog',lambda d:d.dismiss())
    page.locator('[data-delete-audit]').first.click()
    assert page.locator('.audit-card').count()==5
    page.once('dialog',lambda d:d.accept())
    page.locator('[data-delete-audit]').first.click()
    assert page.locator('.audit-card').count()==4
    assert page.locator(f'.audit-card[data-open="{first_id}"]').count()==0
    page.reload()
    page.locator('#year').select_option('2027')
    page.get_by_role('button',name='◫ Calendário',exact=True).click()
    assert page.locator('.audit-card').count()==4
    page.locator('#demo-profile').select_option(label='Ana — Usuário')
    assert page.locator('[data-delete-audit]').count()==0
    assert page.evaluate("async()=>{try{await api('audits/'+data.audits[0].id,'DELETE',{});return false;}catch(e){return e.message.includes('Administrador');}}")
    assert not errors,errors
    browser.close()
print('PASS: calendar colors and norm filter, cancellation/deletion, admin corrections, regular readonly access and revalidated completion.')
