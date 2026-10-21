import copy
import http.cookiejar
import json
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer
from pathlib import Path
import server


class AccessWorkflow(unittest.TestCase):
    def setUp(self):
        self.original_db,self.original_preload=server.DB,server.PRELOAD_REQUIREMENTS
        self.temp=tempfile.TemporaryDirectory()
        server.DB=Path(self.temp.name)/'access.db'
        server.PRELOAD_REQUIREMENTS=False
        self.http=ThreadingHTTPServer(('127.0.0.1',0),server.Handler)
        threading.Thread(target=self.http.serve_forever,daemon=True).start()
        self.url=f'http://127.0.0.1:{self.http.server_port}'
        self.admin=self.client()
        self.call(self.admin,'setup',dict(username='admin',full_name='Administrador',email='admin@example.com',password='Example-admin-password'))
        self.auditors=[]
        for name in ['Ana','Bruno']:
            self.auditors.append(self.call(self.admin,'auditors',dict(name=name,qualifications={'ISO 9001:2015':['leader','auditor']})))
        self.call(self.admin,'areas',{'name':'Qualidade'})
        self.macro=self.call(self.admin,'macroprocesses',dict(name='Gestão'))
        self.process=self.call(self.admin,'processes',dict(name='Qualidade',macroprocess_id=self.macro['id'],norms=list(server.NORMS)))
        self.req=self.call(self.admin,'requirements',dict(norm='ISO 9001:2015',code='8.4',title='Fornecimento externo'))
        self.own=self.make_audit(0)
        self.other=self.make_audit(1)
        result=self.call(self.admin,'users',dict(username='ana.rede',full_name='Ana da Rede',email='ana@example.com',role='user',auditor_id=self.auditors[0]['id']))
        self.password=result['temporary_password']
        self.regular=self.client()
        self.call(self.regular,'login',dict(username='ANA.REDE',password=self.password))

    def tearDown(self):
        self.http.shutdown();self.http.server_close();self.temp.cleanup()
        server.DB,server.PRELOAD_REQUIREMENTS=self.original_db,self.original_preload

    @staticmethod
    def client():
        return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def call(self,client,path,body=None,method=None,headers=None):
        request=urllib.request.Request(self.url+'/api/'+path,data=json.dumps(body).encode() if body is not None else None,method=method,headers={'Content-Type':'application/json',**(headers or {})})
        with client.open(request) as response:return json.load(response)

    def fails(self,status,client,path,body=None,method=None,headers=None):
        with self.assertRaises(urllib.error.HTTPError) as ctx:self.call(client,path,body,method,headers)
        self.assertEqual(ctx.exception.code,status)

    def make_audit(self,leader):
        aid=self.call(self.admin,'audits',dict(audit_type='I',macroprocess_ids=[self.macro['id']],process_ids=[self.process['id']],year=2027,month=3,area='Qualidade',norms=['ISO 9001:2015'],lead=self.auditors[leader]['name'],lead_id=self.auditors[leader]['id'],team_ids=[self.auditors[1-leader]['id']],requirement_ids=[self.req['id']],requirements='8.4',status='Programada'))['id']
        return next(a for a in self.call(self.admin,'data')['audits'] if a['id']==aid)

    def test_edit_findings_authorization_and_revalidate_completion(self):
        plan={k:self.own[k] for k in ['lead','lead_id','team_ids','norms','requirement_ids','requirements']}
        plan.update(date='2027-03-10',confirmed=True)
        self.own.update(plan=plan,status='Planejada')
        self.call(self.regular,'audits/'+str(self.own['id']),self.own,'PUT')
        self.own=self.call(self.regular,'data')['audits'][0]
        body=dict(audit_id=self.own['id'],kind='Conformidade',requirement_ids=[self.req['id']],evidence='Evidência original')
        fid=self.call(self.regular,'findings',body)['id']
        path='findings/'+str(fid)
        self.call(self.regular,path,dict(body,evidence='Evidência corrigida'),'PUT')
        self.fails(401,self.client(),path,body,'PUT')
        self.fails(400,self.admin,path,dict(body,audit_id=self.other['id']),'PUT')
        # Another leader cannot edit an existing finding even when sending its current audit ID.
        result=self.call(self.admin,'users',dict(username='bruno.rede',full_name='Bruno',email='bruno@example.com',role='user',auditor_id=self.auditors[1]['id']))
        other=self.client();self.call(other,'login',dict(username='bruno.rede',password=result['temporary_password']))
        self.fails(403,other,path,body,'PUT')
        observation=dict(audit_id=self.own['id'],kind='Observação',description='Verificar periodicamente')
        self.call(self.regular,path,observation,'PUT')
        state=self.call(self.regular,'data');finding=next(f for f in state['findings'] if f['id']==fid)
        self.assertNotIn('requirement_ids',finding)
        final=dict(self.own,status='Concluída',report={})
        self.fails(400,self.regular,'audits/'+str(self.own['id']),final,'PUT')
        self.call(self.regular,path,body,'PUT')
        self.call(self.regular,'audits/'+str(self.own['id']),final,'PUT')
        self.fails(403,self.regular,path,dict(body,evidence='Alteração após conclusão'),'PUT')
        self.fails(403,self.regular,'findings',body)
        self.fails(403,self.regular,path,{'audit_id':self.own['id']},'DELETE')
        self.fails(403,self.regular,'audits/'+str(self.own['id']),dict(final,status='Em execução'),'PUT')
        self.assertEqual(len(self.call(self.regular,'data')['findings']),1)
        self.call(self.admin,path,dict(body,evidence='Correção administrativa'),'PUT')
        reopened=next(a for a in self.call(self.admin,'data')['audits'] if a['id']==self.own['id'])
        self.assertEqual(reopened['status'],'Em execução')
        self.call(self.admin,'audits/'+str(self.own['id']),final,'PUT')
        self.call(self.admin,path,{},'DELETE')
        state=self.call(self.admin,'data')
        self.assertEqual(len(state['findings']),0)
        self.assertEqual(next(a for a in state['audits'] if a['id']==self.own['id'])['status'],'Em execução')
        self.fails(400,self.admin,'audits/'+str(self.own['id']),final,'PUT')
        self.fails(404,self.admin,path,{},'DELETE')
        draft_id=self.call(self.regular,'findings',body)['id']
        self.fails(403,other,'findings/'+str(draft_id),{},'DELETE')
        self.call(self.regular,'findings/'+str(draft_id),{},'DELETE')
        self.assertEqual(len(self.call(self.regular,'data')['findings']),0)

    def test_delete_audit_admin_only_and_preserves_sequences(self):
        path='audits/'+str(self.own['id'])
        self.fails(401,self.client(),path,{},'DELETE')
        self.fails(403,self.regular,path,{},'DELETE')
        with server.connection() as con:
            con.execute('INSERT INTO findings(audit_id,payload) VALUES (?,?)',(self.own['id'],json.dumps(dict(kind='Observação',description='Anotação',audit_id=self.own['id']))))
        self.call(self.admin,path,{},'DELETE')
        state=self.call(self.admin,'data')
        self.assertEqual([a['id'] for a in state['audits']],[self.other['id']])
        self.assertFalse(state['findings'])
        self.assertTrue(state['auditors'])
        self.assertTrue(state['requirements'])
        self.fails(404,self.admin,path,{},'DELETE')
        new=self.make_audit(0)
        self.assertEqual(new['code'],'AUDI-I-003-2027')
        self.call(self.admin,'audits/'+str(new['id']),{},'DELETE')
        self.call(self.admin,'audits/'+str(self.other['id']),{},'DELETE')
        restarted=self.make_audit(0)
        self.assertEqual(restarted['code'],'AUDI-I-001-2027')
        following=self.make_audit(0)
        self.assertEqual(following['code'],'AUDI-I-002-2027')

    def test_supplier_program_without_internal_fields_and_rqf(self):
        body=dict(self.own,audit_type='F',year=2031,supplier_name='Fornecedor Teste',supplier_cnpj='12.345.678/0001-95',supplier_address='Rua Teste, 100',supply_scope='Componentes')
        for key in ['area','area_names','macroprocess_ids','process_ids','macroprocesses','processes','id','code']:
            body.pop(key,None)
        created=self.call(self.admin,'audits',body)
        supplier=next(a for a in self.call(self.admin,'data')['audits'] if a['id']==created['id'])
        self.assertEqual(supplier['code'],'AUDI-F-001-2031')
        self.assertEqual(supplier['area_names'],[])
        self.assertEqual(supplier['macroprocess_ids'],[])
        plan={key:copy.deepcopy(supplier[key]) for key in ['lead_id','lead','team_ids','team','norms','requirement_ids','requirements']}
        plan.update(date='2031-03-10',confirmed=True)
        supplier.update(plan=plan,status='Planejada')
        self.call(self.admin,'audits/'+str(supplier['id']),supplier,'PUT')
        supplier=next(a for a in self.call(self.admin,'data')['audits'] if a['id']==created['id'])
        self.call(self.regular,'findings',dict(audit_id=supplier['id'],kind='Conformidade',requirement_ids=[self.req['id']],evidence='Fornecimento verificado'))
        supplier.update(status='Concluída',report=dict(conclusion='Fornecedor qualificado',qualification_result='one_year',rqf=' RQF-2031-001 '))
        self.call(self.regular,'audits/'+str(supplier['id']),supplier,'PUT')
        saved=next(a for a in self.call(self.admin,'data')['audits'] if a['id']==created['id'])
        self.assertEqual(saved['report']['rqf'],'RQF-2031-001')
        self.assertEqual(saved['report']['conclusion'],'Fornecedor qualificado')
        self.assertEqual(saved['report']['qualification_result'],'one_year')
        for result in ['not_qualified','one_year','three_years']:
            self.call(self.admin,'audits/'+str(supplier['id']),dict(supplier,report=dict(qualification_result=result,conclusion='',rqf='RQF-2031-001')),'PUT')
        for invalid in ['',None,'qualified','five_years',[]]:
            self.fails(400,self.admin,'audits/'+str(supplier['id']),dict(supplier,report=dict(qualification_result=invalid,conclusion='Observações')),'PUT')

    def test_process_norms_multiple_selection_and_history(self):
        body=dict(name='Processo qualificado',macroprocess_id=self.macro['id'],norms=['ISO 45001'])
        for ns in [[],['Norma inexistente'],['ISO 45001','ISO 45001']]:
            self.fails(400,self.admin,'processes',dict(body,norms=ns))
        pid=self.call(self.admin,'processes',body)['id']
        self.fails(403,self.regular,'processes/'+str(pid),body,'PUT')
        process=next(p for p in self.call(self.admin,'data')['processes'] if p['id']==pid)
        self.assertEqual(process['norms'],['ISO 45001'])
        self.assertEqual(process['history'][0]['action'],'Cadastro')
        self.fails(400,self.admin,'audits',dict(self.own,process_ids=[pid]))
        self.call(self.admin,'processes/'+str(pid),dict(body,norms=['ISO 45001','ISO 9001:2015']),'PUT')
        process=next(p for p in self.call(self.admin,'data')['processes'] if p['id']==pid)
        self.assertEqual(len(process['history']),2)
        self.assertEqual(process['history'][1]['before']['norms'],['ISO 45001'])
        self.assertEqual(process['history'][1]['by'],'Administrador')
        self.call(self.admin,'audits',dict(self.own,process_ids=[pid]))

    def test_process_mandatory_requirements_enforced_in_program_and_plan(self):
        other=self.call(self.admin,'requirements',dict(norm='ISO 9001:2015',code='6.1',title='Riscos'))
        foreign=self.call(self.admin,'requirements',dict(norm='ISO 45001',code='4.1',title='Contexto'))
        body=dict(name='Processo obrigatório',macroprocess_id=self.macro['id'],norms=['ISO 9001:2015'],mandatory_requirement_ids=[other['id']])
        for ids in [[foreign['id']],[999999],[other['id'],other['id']],'6.1']:
            self.fails(400,self.admin,'processes',dict(body,mandatory_requirement_ids=ids))
        pid=self.call(self.admin,'processes',body)['id']
        audit=dict(self.own,process_ids=[pid])
        self.fails(400,self.admin,'audits',audit)
        audit['requirement_ids']=[self.req['id'],other['id']]
        created=self.call(self.admin,'audits',audit)
        audit=next(a for a in self.call(self.admin,'data')['audits'] if a['id']==created['id'])
        plan={key:copy.deepcopy(audit[key]) for key in ['lead_id','lead','team_ids','team','norms','requirement_ids','requirements']}
        plan.update(date='2027-03-10',confirmed=False,requirement_ids=[self.req['id']])
        self.fails(400,self.admin,'audits/'+str(audit['id']),dict(audit,plan=plan,status='Plano em rascunho'),'PUT')
        plan['requirement_ids'].append(other['id'])
        self.call(self.admin,'audits/'+str(audit['id']),dict(audit,plan=plan,status='Plano em rascunho'),'PUT')

    def test_admin_catalog_edit_delete_and_existing_audit_history(self):
        macro_path='macroprocesses/'+str(self.macro['id'])
        process_path='processes/'+str(self.process['id'])
        uid=next(u['id'] for u in self.call(self.admin,'data')['users'] if u['username']=='ana.rede')
        user_path='users/'+str(uid)
        body=dict(username='ana.nova',full_name='Ana corrigida',email='ana.nova@example.com',role='user',auditor_id=self.auditors[0]['id'])
        for path,method,entry in [(macro_path,'PUT',{'name':'Revisado'}),(macro_path,'DELETE',{}),(process_path,'DELETE',{}),(user_path,'PUT',body),(user_path,'DELETE',{})]:
            self.fails(403,self.regular,path,entry,method)
        self.call(self.admin,macro_path,{'name':'Gestão revisada'},'PUT')
        state=self.call(self.admin,'data')
        self.assertEqual(state['macroprocesses'][0]['name'],'Gestão revisada')
        self.assertEqual(state['audits'][0]['macroprocesses'],'Gestão')
        self.call(self.admin,user_path,body,'PUT')
        changed=self.client();self.call(changed,'login',dict(username='ANA.NOVA',password=self.password))
        self.assertEqual(self.call(changed,'data')['current_user']['full_name'],'Ana corrigida')
        self.fails(400,self.admin,user_path,dict(body,username='admin'),'PUT')
        admin_id=next(u['id'] for u in self.call(self.admin,'data')['users'] if u['role']=='admin')
        self.fails(400,self.admin,'users/'+str(admin_id),{},'DELETE')
        self.fails(400,self.admin,'users/'+str(admin_id),dict(body,username='admin'),'PUT')
        self.call(self.admin,user_path,{},'DELETE')
        self.fails(401,changed,'data')
        self.call(self.admin,process_path,{},'DELETE')
        self.assertEqual(self.call(self.admin,'data')['processes'],[])
        self.assertEqual(len(self.call(self.admin,'data')['audits']),2)
        self.call(self.admin,macro_path,{},'DELETE')
        self.assertEqual(self.call(self.admin,'data')['macroprocesses'],[])
        self.fails(404,self.admin,macro_path,{},'DELETE')
        self.fails(404,self.admin,process_path,{},'DELETE')
        restored=self.call(self.admin,'macroprocesses',{'name':'Gestão revisada'})
        self.assertEqual(restored['id'],self.macro['id'])
        self.call(self.admin,'processes',dict(name='Novo processo',macroprocess_id=restored['id'],norms=['ISO 9001:2015']))
        self.call(self.admin,macro_path,{},'DELETE')
        self.assertEqual(self.call(self.admin,'data')['processes'],[])

    def test_logo_settings_admin_only_and_shared_with_leaders(self):
        import base64, struct, zlib
        def chunk(kind,data):
            return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
        raw=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',20,10,8,2,0,0,0))+chunk(b'IDAT',zlib.compress((b'\0'+b'\x00\x40\x80'*20)*10))+chunk(b'IEND',b'')
        logo='data:image/png;base64,'+base64.b64encode(raw).decode()
        self.fails(401,self.client(),'settings',{'logo':logo},'PUT')
        self.fails(403,self.regular,'settings',{'logo':logo},'PUT')
        for invalid in ['https://example.com/logo.png','data:image/png;base64,garbage','x'*700001,None]:
            self.fails(400,self.admin,'settings',{'logo':invalid},'PUT')
        self.call(self.admin,'settings',{'logo':logo},'PUT')
        self.assertEqual(self.call(self.regular,'data')['settings']['logo'],logo)
        # The former draft phase is upgraded without changing the audit's identity.
        with server.connection() as con:
            legacy=dict(self.own,status='Plano em rascunho',plan=dict(lead='Ana',date='2027-03-10',confirmed=False))
            con.execute('UPDATE audits SET payload=? WHERE id=?',(json.dumps(legacy),self.own['id']))
        current=next(a for a in self.call(self.admin,'data')['audits'] if a['id']==self.own['id'])
        self.assertEqual(current['status'],'Planejada');self.assertTrue(current['plan']['confirmed'])
        self.assertEqual(current['code'],self.own['code'])

    def test_administrator_can_edit_auditors_preserving_links(self):
        aid=self.auditors[0]['id'];path='auditors/'+str(aid)
        body=dict(name='Ana corrigida',qualifications={'ISO 9001:2015':['leader','auditor','observer'],'ISO 45001':['auditor']})
        self.fails(403,self.regular,path,body,'PUT')
        self.call(self.admin,path,body,'PUT')
        state=self.call(self.admin,'data');person=next(a for a in state['auditors'] if a['id']==aid)
        self.assertEqual(person['name'],'Ana corrigida')
        self.assertIn('observer',person['qualifications']['ISO 9001:2015'])
        self.assertEqual(next(u for u in state['users'] if u['username']=='ana.rede')['auditor_id'],aid)
        self.assertEqual(next(a for a in state['audits'] if a['id']==self.own['id'])['lead_id'],aid)
        for invalid in [dict(body,name='Bruno'),dict(body,qualifications={}),dict(body,qualifications={'Norma inexistente':['leader']}),dict(body,name='')]:
            self.fails(400,self.admin,path,invalid,'PUT')
        self.fails(404,self.admin,'auditors/999999',body,'PUT')

    def test_authentication_and_passwords_are_not_exposed(self):
        anonymous=self.client()
        self.fails(401,anonymous,'data')
        with anonymous.open(self.url+'/healthz') as response:
            self.assertEqual(response.status,200)
        self.fails(401,anonymous,'login',dict(username='admin',password='Incorrect-password'))
        self.assertFalse(self.call(anonymous,'session')['needs_setup'])
        self.fails(403,anonymous,'setup',dict(username='takeover',full_name='X',email='x@example.com',password='Example-password'))
        state=self.call(self.admin,'data')
        for user in state['users']:
            self.assertNotIn('password_hash',user)
            self.assertNotIn('password',user)
        with server.connection() as con:
            hashes=[x for (x,) in con.execute('SELECT password_hash FROM users')]
        self.assertTrue(all('$' in value and self.password not in value for value in hashes))

    def test_regular_user_can_only_see_owned_lead_audits(self):
        state=self.call(self.regular,'data')
        self.assertEqual([a['id'] for a in state['audits']],[self.own['id']])
        self.assertEqual(state['users'],[])
        self.assertEqual(state['current_user']['role'],'user')
        for path,body in [('macroprocesses',{'name':'Forbidden'}),('processes',{}),('areas',{'name':'Forbidden'}),('norms',{'name':'Forbidden'}),('auditors',{}),('requirements',{}),('users',{'role':'admin'}),('audits',self.other)]:
            self.fails(403,self.regular,path,body)
        self.fails(403,self.regular,'audits/'+str(self.other['id']),self.other,'PUT')
        self.fails(403,self.regular,'requirements/'+str(self.req['id']),{'title':'Alteração indevida'},'PUT')

    def test_leader_can_generate_plan_confirm_and_report_without_editing_program(self):
        audit=copy.deepcopy(self.own)
        plan={key:copy.deepcopy(audit[key]) for key in ['lead_id','lead','team_ids','team','norms','requirement_ids','requirements']}
        plan.update(date='2027-03-10',periods=[dict(date='2027-03-10',start='08:00',end='12:00')],confirmed=False)
        audit.update(plan=plan,status='Plano em rascunho')
        self.call(self.regular,'audits/'+str(audit['id']),audit,'PUT')
        current=self.call(self.regular,'data')['audits'][0]
        for key,value in [('area','Outra'),('month',4),('lead_id',self.auditors[1]['id']),('role','admin')]:
            self.fails(403,self.regular,'audits/'+str(audit['id']),dict(current,**{key:value}),'PUT')
        for key,value in [('norms',['ISO 45001']),('team_ids',[]),('requirements','Critério alterado'),('location','Outro local'),('manager_email','outro@example.com'),('manager_name','Outro gestor')]:
            forbidden=copy.deepcopy(current);forbidden['plan'][key]=value
            self.fails(403,self.regular,'audits/'+str(audit['id']),forbidden,'PUT')
        dated=copy.deepcopy(current);dated['plan'].update(date='2027-03-11',periods=[dict(date='2027-03-11',start='13:00',end='17:00')])
        self.call(self.regular,'audits/'+str(audit['id']),dated,'PUT')
        current=self.call(self.regular,'data')['audits'][0]
        changed=copy.deepcopy(current);changed['plan']['lead_id']=self.auditors[1]['id']
        self.fails(403,self.regular,'audits/'+str(audit['id']),changed,'PUT')
        current['plan']['confirmed']=True;current['status']='Planejada'
        self.call(self.regular,'audits/'+str(audit['id']),current,'PUT')
        finding=dict(requirement_ids=[self.req['id']],audit_id=audit['id'],kind='Não conformidade',area='Qualidade',description='Registro ausente',requirements='8.4',evidence='Registro verificado',pac='PAC-001')
        self.call(self.regular,'findings',finding)
        self.fails(403,self.regular,'findings',dict(finding,audit_id=self.other['id']))
        current=self.call(self.regular,'data')['audits'][0]
        current.update(status='Concluída',report={'participants':'Ana','conclusion':'Auditoria encerrada'})
        self.call(self.regular,'audits/'+str(audit['id']),current,'PUT')
        self.assertEqual(self.call(self.regular,'data')['audits'][0]['status'],'Concluída')

    def test_users_validate_link_and_duplicate_network_name(self):
        self.fails(400,self.admin,'users',dict(username='unlinked',full_name='Sem vínculo',email='x@example.com',role='user'))
        self.fails(400,self.admin,'users',dict(username='ANA.REDE',full_name='Duplicado',email='x@example.com',role='user',auditor_id=self.auditors[0]['id']))
        self.fails(400,self.admin,'users',dict(username='bad.email',full_name='Nome',email='inválido',role='admin'))
        result=self.call(self.admin,'users',dict(username='second.admin',full_name='Outro administrador',email='other@example.com',role='admin'))
        self.assertEqual(result['user']['role'],'admin')
        self.assertTrue(result['temporary_password'])
        another=self.client();self.call(another,'login',dict(username='second.admin',password=result['temporary_password']))
        self.call(another,'areas',dict(name='Permitido para administrador'))

    def test_password_change_logout_and_csrf(self):
        self.fails(403,self.regular,'password',dict(current_password='Wrong-password',new_password='New-test-password'))
        self.call(self.regular,'password',dict(current_password=self.password,new_password='New-test-password'))
        another=self.client()
        self.fails(401,another,'login',dict(username='ana.rede',password=self.password))
        self.call(another,'login',dict(username='ana.rede',password='New-test-password'))
        self.fails(403,self.admin,'areas',dict(name='Cross site'),headers={'Origin':'https://another.example'})
        self.call(self.regular,'logout',{})
        self.fails(401,self.regular,'data')
        self.assertIsNone(self.call(self.regular,'session')['user'])
        with server.connection() as con:
            con.execute('UPDATE sessions SET expires=0')
        self.fails(401,another,'data')
