"""Calendar exports honor multiple norms, year, search and user visibility; long rows paginate."""
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path
from playwright.sync_api import sync_playwright
root=Path('/tmp/nuclaud-preview');html=(root/'NUCLAUD-v12.html').read_text()
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=browser.new_page();page.set_default_timeout(8000);errors=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('http://127.0.0.1:8123/**',lambda r:r.fulfill(content_type='text/html',body=html))
    page.goto('http://127.0.0.1:8123/')
    page.evaluate('''async()=>{
      const c=document.createElement('canvas');c.width=160;c.height=50;const ctx=c.getContext('2d');ctx.fillStyle='#174b80';ctx.fillRect(0,0,160,50);ctx.fillStyle='white';ctx.fillText('LOGO TESTE',20,30);await api('settings','PUT',{logo:c.toDataURL('image/png')});
      await api('areas','POST',{name:'Qualidade'});await api('areas','POST',{name:'SMS'});
      const m=await api('macroprocesses','POST',{name:'Gestão'}),process=await api('processes','POST',{name:'Processo',macroprocess_id:m.id,norms:[...norms]});
      const qualification=Object.fromEntries(norms.map(n=>[n,['leader','auditor']]));
      const ana=await api('auditors','POST',{name:'Ana',qualifications:qualification}),bruno=await api('auditors','POST',{name:'Bruno',qualifications:qualification});
      const groups=[['ISO 9001:2015'],['ISO 14001','ISO 45001'],['ANSN 1.16']];
      window.programRecords=[];
      for(let i=0;i<4;i++){
       const ns=groups[i%3],state=await api('data'),rs=state.requirements.filter(r=>ns.includes(r.norm)&&(i===0?/^\\d+\\.\\d+(?:\\.\\d+)?$/.test(r.code):r.code==='4.1'));
       const lead=i===0||i===3?ana:bruno;
       const body={audit_type:'I',year:i===3?2028:2027,month:[3,1,6,3][i],area:i===1?'SMS':'Qualidade',area_names:[i===1?'SMS':'Qualidade'],macroprocess_ids:[m.id],process_ids:[process.id],macroprocesses:'Gestão',processes:'Gestão › Processo',lead_id:lead.id,lead:i===0||i===3?'Ana':'Bruno',team_ids:[],norms:ns,requirement_ids:rs.map(r=>r.id),requirements:rs.map(r=>r.norm+' · '+r.code+' — '+r.title).join('\\n'),status:'Programada'};
       const result=await api('audits','POST',body);programRecords.push({...body,id:result.id});
      }
      await api('users','POST',{username:'ana.rede',full_name:'Ana',email:'ana@example.com',role:'user',auditor_id:ana.id});await load();
    }''')
    page.locator('#year').select_option('2027');page.get_by_role('button',name='◫ Calendário',exact=True).click()
    def norms(values):
        if not page.locator('#calendar-norm').evaluate('(el)=>el.open'):page.locator('#calendar-norm summary').click()
        page.locator('#calendar-norm').get_by_label('Todas as normas',exact=True).check()
        for value in values:page.locator('#calendar-norm').get_by_label(value,exact=True).check()
        page.locator('#close-calendar-filter').click()
    def export(name):
        with page.expect_download() as result:page.get_by_role('button',name='Gerar PDF do programa',exact=True).click()
        assert '2027' in result.value.suggested_filename
        path=root/(name+'.pdf');result.value.save_as(str(path))
        text=subprocess.check_output(['pdftotext','-layout',str(path),'-'],text=True)
        assert 'Programa de Auditorias Internas' in text and '2027' in text
        return text,path
    assert page.locator('.audit-card').count()==3
    all_text,all_pdf=export('program-all')
    for code in ['AUDI-I-001-2027','AUDI-I-002-2027','AUDI-I-003-2027']:assert code in all_text
    assert 'AUDI-I-001-2028' not in all_text
    assert all_text.index('AUDI-I-002-2027')<all_text.index('AUDI-I-001-2027')<all_text.index('AUDI-I-003-2027')
    norms(['ISO 9001:2015']);assert page.locator('.audit-card').count()==1
    text,_=export('program-one');assert 'AUDI-I-001-2027' in text and 'AUDI-I-002-2027' not in text
    norms(['ISO 9001:2015','ISO 45001']);assert page.locator('.audit-card').count()==2
    text,_=export('program-multiple');assert 'AUDI-I-001-2027' in text and 'AUDI-I-002-2027' in text and 'AUDI-I-003-2027' not in text
    norms(['ISO 14001','ISO 45001']);assert page.locator('.audit-card').count()==1
    text,_=export('program-overlap');assert 'AUDI-I-002-2027' in text and 'Total: 1' in text
    norms([]);page.locator('#search').fill('SMS');assert page.locator('.audit-card').count()==1
    text,_=export('program-search');assert 'AUDI-I-002-2027' in text and 'AUDI-I-001-2027' not in text
    page.locator('#search').fill('Nenhum resultado');assert page.locator('.audit-card').count()==0
    text,_=export('program-empty');assert 'Nenhuma auditoria encontrada' in text and 'Total: 0' in text
    page.locator('#search').fill('');page.locator('#demo-profile').select_option(label='Ana — Usuário')
    assert page.locator('.audit-card').count()==1
    text,_=export('program-user');assert 'AUDI-I-001-2027' in text and 'AUDI-I-002-2027' not in text
    assert not errors,errors
    browser.close()

bbox=root/'program-all.xhtml';subprocess.run(['pdftotext','-bbox-layout',str(all_pdf),str(bbox)],check=True)
pages=ET.parse(bbox).findall('.//{http://www.w3.org/1999/xhtml}page')
assert len(pages)>1
for page in pages:
    w,h=float(page.get('width')),float(page.get('height'));assert 841<w<843 and 594<h<596
    for word in page.findall('.//{http://www.w3.org/1999/xhtml}word'):
        assert 15<float(word.get('xMin'))<float(word.get('xMax'))<w-15,(word.text,word.attrib)
        assert 15<float(word.get('yMin'))<float(word.get('yMax'))<h-15,(word.text,word.attrib)
images=subprocess.check_output(['pdfimages','-list',str(all_pdf)],text=True).splitlines()[2:]
assert {int(line.split()[0]) for line in images if line.strip()}==set(range(1,len(pages)+1))
print('PASS: all/single/multiple norms, OR overlap, year/search/empty filters, own-audit permissions, logo and long-row landscape A4 pagination.')

# Check light row backgrounds: text drawing must not turn following cell fills dark.
from PIL import Image
subprocess.run(['pdftoppm','-f','1','-singlefile','-r','72','-png',str(all_pdf),str(root/'program-first-page')],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
with Image.open(root/'program-first-page.png') as image:
    x=14
    for width in [20,34,50,37,43,65,20]:
        pixel=image.convert('RGB').getpixel((round((x+1)*72/25.4),round(60.5*72/25.4)))
        assert min(pixel)>220,pixel
        x+=width
