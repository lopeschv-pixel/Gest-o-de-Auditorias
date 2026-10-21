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
        self.process=self.call(self.admin,'processes',dict(name='Qualidade',macroprocess_id=self.macro['id']))
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
