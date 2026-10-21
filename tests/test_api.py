import json
import tempfile
import threading
import unittest
import urllib.request
import urllib.error
import http.cookiejar
from pathlib import Path
from http.server import ThreadingHTTPServer
import server

class Workflow(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original_db=server.DB
        cls.preload=server.PRELOAD_REQUIREMENTS
        server.PRELOAD_REQUIREMENTS=False
        cls.tmp = tempfile.TemporaryDirectory()
        server.DB = Path(cls.tmp.name) / 'test.db'
        with server.connection() as con:
            for name in ['Auditor líder','Outro auditor']:
                con.execute('INSERT INTO auditors(payload) VALUES (?)',(json.dumps(dict(name=name,qualifications={n:'leader' for n in server.NORMS})),))
            for norm in ['ISO 9001:2015','ANSN 1.16','ASME III']:
                con.execute('INSERT INTO requirements(payload) VALUES (?)',(json.dumps(dict(norm=norm,code='8.4',description='Requisito cadastrado')),))
        with server.connection() as con:
            con.execute("INSERT INTO macroprocesses(name) VALUES ('Gestão')")
            con.execute("INSERT INTO processes(macroprocess_id,name) VALUES (1,'Qualidade')")
        cls.http = ThreadingHTTPServer(('127.0.0.1', 0), server.Handler)
        threading.Thread(target=cls.http.serve_forever, daemon=True).start()
        cls.url = 'http://127.0.0.1:' + str(cls.http.server_port)
        cls.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        body=dict(username='admin',full_name='Administrador teste',email='admin@example.com',password='Example-test-password')
        req=urllib.request.Request(cls.url+'/api/setup',data=json.dumps(body).encode(),headers={'Content-Type':'application/json'})
        with cls.opener.open(req) as response:
            assert response.status==201

    @classmethod
    def tearDownClass(cls):
        cls.http.shutdown()
        cls.http.server_close()
        cls.tmp.cleanup()
        server.PRELOAD_REQUIREMENTS=cls.preload
        server.DB=cls.original_db

    def request(self, path, body=None, method=None):
        req = urllib.request.Request(self.url+path, data=json.dumps(body).encode() if body else None, headers={'Content-Type':'application/json'}, method=method)
        with self.opener.open(req) as r:
            return json.load(r)

    def test_full_workflow_and_pac_validation(self):
        self.request('/api/areas', {'name':'Compras'})
        audit = dict(audit_type='I',macroprocess_ids=[1],process_ids=[1], year=2027, month=3, area='Compras', lead='Auditor líder', norms=['ISO 9001:2015'], requirements='ISO 9001:2015 — 8.4', lead_id=1, team_ids=[], requirement_ids=[1], status='Programada')
        aid = self.request('/api/audits',audit)['id']
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/audits/'+str(aid),dict(audit, report={'conclusion':'Prematuro'}),'PUT')
        audit['plan'] = dict(date='2027-03-10', lead='Outro auditor', team='Equipe', norms=['ANSN 1.16','ASME III'], requirements='ANSN 1.16; ASME III',lead_id=2,team_ids=[],requirement_ids=[2,3], confirmed=True)
        audit['status'] = 'Planejada'
        self.request('/api/audits/'+str(aid),audit,'PUT')
        self.assertEqual(self.request('/api/data')['audits'][0]['code'], 'AUDI-I-001-2027')
        finding = dict(requirement_ids=[2],audit_id=aid, kind='Não conformidade', area='Compras', description='Registro ausente', requirements='ISO 9001:2015 — 8.4', evidence='Amostra do processo de compra', pac='')
        with self.assertRaises(urllib.error.HTTPError) as ctx:
            self.request('/api/findings',finding)
        self.assertEqual(ctx.exception.code,400)
        finding['pac']='PAC-001'
        self.request('/api/findings',finding)
        audit.update(status='Concluída', report={'conclusion':'Auditoria realizada com uma não conformidade.'})
        self.request('/api/audits/'+str(aid),audit,'PUT')
        state=self.request('/api/data')
        self.assertEqual(state['audits'][0]['status'],'Concluída')
        self.assertEqual(state['audits'][0]['lead'],'Auditor líder')
        self.assertEqual(state['audits'][0]['plan']['lead'],'Outro auditor')
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/audits/'+str(aid), dict(audit,plan=dict(audit['plan'],confirmed=False)), 'PUT')
        own_finding=next(f for f in state['findings'] if f['audit_id']==aid)
        self.assertEqual(own_finding['pac'],'PAC-001')
        self.assertEqual(own_finding['audit_id'],aid)
        with server.connection() as con:
            self.assertEqual(con.execute('SELECT count(*) FROM findings WHERE audit_id=?',(aid,)).fetchone()[0],1)

    def test_missing_audit_rejected(self):
        with self.assertRaises(urllib.error.HTTPError) as ctx:
            self.request('/api/findings',dict(audit_id=99999,kind='Observação',area='SMS',description='Verificar registro',requirements='ISO 14001',evidence='Registro analisado'))
        self.assertEqual(ctx.exception.code,400)

    def test_sequences_and_registered_areas(self):
        self.request('/api/areas', {'name':'Qualidade'})
        audit = dict(audit_type='I',macroprocess_ids=[1],process_ids=[1],year=2030,month=5,area='Qualidade',lead='Auditor',norms=['ASME III'],requirements='Requisito',lead_id=1,team_ids=[],requirement_ids=[3],status='Programada')
        first = self.request('/api/audits', audit)
        second = self.request('/api/audits', audit)
        supplier = self.request('/api/audits', dict(audit,audit_type='F'))
        another_year = self.request('/api/audits', dict(audit,year=2031))
        self.assertEqual(first['code'],'AUDI-I-001-2030')
        self.assertEqual(second['code'],'AUDI-I-002-2030')
        self.assertEqual(supplier['code'],'AUDI-F-001-2030')
        self.assertEqual(another_year['code'],'AUDI-I-001-2031')
        for payload in [dict(audit,area='Não cadastrada'),dict(audit,audit_type='ASME')]:
            with self.assertRaises(urllib.error.HTTPError):
                self.request('/api/audits',payload)
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/audits/'+str(first['id']),dict(audit,audit_type='F'),'PUT')
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/areas',{'name':' qualidade '})
        self.request('/api/audits/'+str(first['id']),dict(audit,lead='Novo auditor'),'PUT')
        updated = next(a for a in self.request('/api/data')['audits'] if a['id']==first['id'])
        self.assertEqual(updated['code'],first['code'])
        self.assertNotIn('title',updated)
        self.request('/api/areas', {'name':'Engenharia'})
        self.request('/api/audits/'+str(first['id']),dict(audit,area_names=['Qualidade','Engenharia']),'PUT')
        state=self.request('/api/data')
        updated=next(a for a in state['audits'] if a['id']==first['id'])
        self.assertEqual(updated['area_names'],['Qualidade','Engenharia'])
        self.assertEqual(updated['area'],'Qualidade · Engenharia')
        self.assertNotIn('Qualidade · Engenharia',[a['name'] for a in state['areas']])
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/audits',dict(audit,area_names=['Qualidade','Não cadastrada']))


    def test_catalog_validation_and_qualification_filters(self):
        member = self.request('/api/auditors',dict(name='Somente auditor',qualifications={'ISO 9001:2015':['auditor']}))
        req = self.request('/api/requirements',dict(norm='ISO 14001',code='6.1',title='Aspectos ambientais',description='Aspectos ambientais'))
        for payload in [dict(name='Sem habilitação',qualifications={}),dict(name='Inválido',qualifications={'ASME III':'inventado'})]:
            with self.assertRaises(urllib.error.HTTPError):
                self.request('/api/auditors',payload)
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/requirements',dict(norm='ISO 14001',code='6.1',title='Aspectos ambientais',description='Duplicado'))
        self.request('/api/areas',{'name':'SMS'})
        audit=dict(audit_type='I',macroprocess_ids=[1],process_ids=[1],year=2032,month=6,area='SMS',lead='Auditor líder',requirements='Seleção',norms=['ISO 9001:2015'],lead_id=1,team_ids=[],requirement_ids=[1],status='Programada')
        for payload in [dict(audit,lead_id=member['id']),dict(audit,requirement_ids=[req['id']]),dict(audit,norms=['ISO 9001:2015','ASME III']),dict(audit,team_ids=[1])]:
            with self.assertRaises(urllib.error.HTTPError):
                self.request('/api/audits',payload)
        self.request('/api/audits',dict(audit,team_ids=[member['id']]))
        state=self.request('/api/data')
        self.assertTrue(any(a['id']==member['id'] for a in state['auditors']))
        self.assertTrue(any(r['id']==req['id'] for r in state['requirements']))

    def test_dynamic_norms_and_independent_roles(self):
        self.assertIn('Secretaria Naval', self.request('/api/data')['norms'])
        self.request('/api/norms', {'name':'Norma de teste'})
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/norms', {'name':' norma de teste '})
        leader=self.request('/api/auditors',dict(name='Líder naval',qualifications={'Secretaria Naval':['leader'],'Norma de teste':['leader','auditor']}))
        auditor=self.request('/api/auditors',dict(name='Auditor naval',qualifications={'Secretaria Naval':['auditor']}))
        req=self.request('/api/requirements',dict(norm='Secretaria Naval',code='N1',title='Requisito naval',description='Requisito naval'))
        self.request('/api/requirements',dict(norm='Norma de teste',code='T1',title='Requisito adicional',description='Requisito adicional'))
        self.request('/api/areas',{'name':'Naval'})
        audit=dict(audit_type='I',macroprocess_ids=[1],process_ids=[1],year=2035,month=2,area='Naval',lead='Líder naval',requirements='N1',norms=['Secretaria Naval'],lead_id=leader['id'],team_ids=[auditor['id']],requirement_ids=[req['id']],status='Programada')
        self.request('/api/audits',audit)
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/audits',dict(audit,lead_id=auditor['id'],team_ids=[leader['id']]))
        observer=self.request('/api/auditors',dict(name='Observador naval',qualifications={'Secretaria Naval':['observer']}))
        self.request('/api/audits',dict(audit,observer_ids=[observer['id']]))
        for payload in [dict(audit,observer_ids=[auditor['id']]),dict(audit,team_ids=[observer['id']]),dict(audit,lead_id=observer['id'])]:
            with self.assertRaises(urllib.error.HTTPError):
                self.request('/api/audits',payload)
        another=self.request('/api/auditors',dict(name='Segundo líder naval',qualifications={'Secretaria Naval':['leader']}))
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/audits',dict(audit,lead_id=another['id'],team_ids=[leader['id']]))
        saved=next(a for a in self.request('/api/data')['auditors'] if a['id']==leader['id'])
        self.assertEqual(saved['qualifications']['Secretaria Naval'],['leader'])
        self.assertEqual(saved['qualifications']['Norma de teste'],['leader','auditor'])

    def test_requirement_without_description_and_multiple_periods(self):
        req=self.request('/api/requirements',dict(norm='ISO 45001',code='8.1',title='Controle operacional'))
        self.assertNotIn('description',req)
        self.assertEqual(req['title'],'Controle operacional')
        self.request('/api/areas',{'name':'Operação'})
        audit=dict(audit_type='I',macroprocess_ids=[1],process_ids=[1],year=2037,month=3,area='Operação',lead='Auditor líder',requirements='8.1',norms=['ISO 45001'],lead_id=1,team_ids=[],requirement_ids=[req['id']],status='Programada')
        aid=self.request('/api/audits',audit)['id']
        periods=[dict(date='2037-03-11',start='13:00',end='17:00'),dict(date='2037-03-10',start='08:00',end='12:00')]
        plan=dict(lead='Auditor líder',norms=['ISO 45001'],requirements='8.1',lead_id=1,team_ids=[],requirement_ids=[req['id']],periods=periods,manager_email='gestor@example.com',location='Sala de reunião',confirmed=False)
        self.request('/api/audits/'+str(aid),dict(audit,plan=plan,status='Plano em rascunho'),'PUT')
        saved=next(a for a in self.request('/api/data')['audits'] if a['id']==aid)
        self.assertEqual(saved['plan']['date'],'2037-03-10')
        self.assertEqual(saved['plan']['periods'][1]['start'],'13:00')
        self.assertEqual(saved['plan']['manager_email'],'gestor@example.com')
        self.assertNotIn('description',saved['plan'])
        for invalid in [[],[dict(date='2037-03-10',start='17:00',end='08:00')],[dict(date='2037-03-10',start='08:00',end='12:00'),dict(date='2037-03-10',start='11:00',end='17:00')]]:
            with self.assertRaises(urllib.error.HTTPError):
                self.request('/api/audits/'+str(aid),dict(audit,plan=dict(plan,periods=invalid),status='Plano em rascunho'),'PUT')
        self.assertEqual(server.validate_periods([dict(date='2037-03-10',start='08:00',end='12:00'),dict(date='2037-03-10',start='13:00',end='17:00')])[1]['start'],'13:00')

    def test_official_catalog_preload_is_idempotent_and_preserves_manual_records(self):
        import sqlite3
        with sqlite3.connect(':memory:') as con:
            con.execute('CREATE TABLE requirements (id INTEGER PRIMARY KEY,payload TEXT NOT NULL)')
            manual=dict(norm='ISO 9001:2015',code='8.4',custom='Manter cadastro existente')
            con.execute('INSERT INTO requirements(payload) VALUES (?)',(json.dumps(manual),))
            added=server.seed_requirements(con)
            self.assertEqual(added,338)
            self.assertEqual(server.seed_requirements(con),0)
            rows=server.catalog(con,'requirements')
            self.assertEqual(len(rows),339)
            self.assertTrue(all(r.get('title') for r in rows))
            self.assertEqual(next(r for r in rows if r['norm']=='ISO 9001:2015' and r['code']=='8.4')['title'],'Controle de fornecimento externo')
            self.assertEqual(next(r for r in rows if r['norm']=='ISO 9001:2015' and r['code']=='8.4')['custom'],manual['custom'])
            self.assertEqual({r['norm'] for r in rows},{'ISO 9001:2015','ISO 14001','ISO 45001','ANSN 1.16'})
            self.assertTrue(any(r['norm']=='ANSN 1.16' and r['code']=='4.13.2.2' for r in rows))
            self.assertTrue(all('description' not in r for r in rows))

    def test_administrator_can_edit_requirement_title(self):
        self.request('/api/requirements/1',{'title':'Título personalizado'},'PUT')
        state=self.request('/api/data')
        req=next(r for r in state['requirements'] if r['id']==1)
        self.assertEqual(req['title'],'Título personalizado')
        self.assertEqual(req['code'],'8.4')
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/requirements/1',{'title':''},'PUT')
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/requirements',{'norm':'ISO 9001:2015','code':'CUSTOM-MISSING-TITLE'})

    def test_findings_classification_fields_and_supplier_only_conclusion(self):
        self.request('/api/areas',{'name':'Relatórios'})
        def make(kind):
            plan=dict(date='2038-03-10',lead='Auditor líder',lead_id=1,team_ids=[2],norms=['ISO 9001:2015'],requirements='8.4',requirement_ids=[1],confirmed=True)
            audit=dict(audit_type=kind,year=2038,month=3,area='Relatórios',macroprocess_ids=[1],process_ids=[1],lead='Auditor líder',lead_id=1,team_ids=[2],norms=['ISO 9001:2015'],requirements='8.4',requirement_ids=[1],plan=plan,status='Planejada')
            aid=self.request('/api/audits',audit)['id']
            return next(a for a in self.request('/api/data')['audits'] if a['id']==aid)
        audit=make('I')
        conform=dict(audit_id=audit['id'],kind='Conformidade',description='Critério atendido',requirement_ids=[1],evidence='Registro analisado')
        self.request('/api/findings',conform)
        for invalid in [dict(conform,evidence=''),dict(conform,requirement_ids=[99999]),dict(conform,kind='Constatação'),dict(conform,kind='Não conformidade',pac='')]:
            with self.assertRaises(urllib.error.HTTPError):self.request('/api/findings',invalid)
        self.request('/api/findings',dict(conform,kind='Não conformidade',pac='PAC-TEST'))
        for kind in ['Observação','Oportunidade de melhoria']:
            self.request('/api/findings',dict(audit_id=audit['id'],kind=kind,description='Anotação registrada',evidence='OCULTO',pac='OCULTO',requirements='OCULTO'))
        findings=[f for f in self.request('/api/data')['findings'] if f['audit_id']==audit['id']]
        self.assertEqual(len(findings),4)
        self.assertTrue(all(f['auditor_name']=='Auditor líder' for f in findings))
        for finding in findings[2:]:
            for key in ['evidence','pac','requirements','requirement_ids']:self.assertNotIn(key,finding)
        self.request('/api/audits/'+str(audit['id']),dict(audit,status='Concluída',report={'participants':'Participantes','conclusion':'Remover campo interno'}),'PUT')
        saved=next(a for a in self.request('/api/data')['audits'] if a['id']==audit['id'])
        self.assertNotIn('conclusion',saved['report'])
        supplier=make('F')
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/audits/'+str(supplier['id']),dict(supplier,status='Concluída',report={'participants':'Equipe'}),'PUT')
        self.request('/api/audits/'+str(supplier['id']),dict(supplier,status='Concluída',report={'participants':'Equipe','conclusion':'Fornecedor avaliado'}),'PUT')

    def test_macroprocess_and_process_registration_and_required_links(self):
        first=self.request('/api/macroprocesses',{'name':'Operações'})
        second=self.request('/api/macroprocesses',{'name':'Suporte'})
        one=self.request('/api/processes',{'name':'Inspeção','macroprocess_id':first['id']})
        two=self.request('/api/processes',{'name':'Inspeção','macroprocess_id':second['id']})
        for path,body in [('macroprocesses',{'name':' operações '}),('processes',{'name':'inspeção','macroprocess_id':first['id']}),('processes',{'name':'Inválido','macroprocess_id':99999})]:
            with self.assertRaises(urllib.error.HTTPError):self.request('/api/'+path,body)
        self.request('/api/areas',{'name':'Escopos'})
        audit=dict(audit_type='I',year=2040,month=5,area='Escopos',lead='Auditor líder',lead_id=1,team_ids=[],norms=['ISO 9001:2015'],requirements='8.4',requirement_ids=[1],status='Programada',macroprocess_ids=[first['id'],second['id']],process_ids=[one['id'],two['id']])
        aid=self.request('/api/audits',audit)['id']
        state=self.request('/api/data');saved=next(a for a in state['audits'] if a['id']==aid)
        self.assertEqual(saved['macroprocesses'],'Operações · Suporte')
        self.assertIn('Operações › Inspeção',saved['processes']);self.assertIn('Suporte › Inspeção',saved['processes'])
        self.assertTrue(any(p['id']==one['id'] and p['macroprocess_id']==first['id'] for p in state['processes']))
        for invalid in [dict(audit,macroprocess_ids=[]),dict(audit,process_ids=[]),dict(audit,process_ids=[one['id']]),dict(audit,macroprocess_ids=[first['id']],process_ids=[two['id']])]:
            with self.assertRaises(urllib.error.HTTPError):self.request('/api/audits',invalid)

    def test_ui_is_served(self):
        with urllib.request.urlopen(self.url+'/') as r:
            self.assertIn('NUCLAUD',r.read().decode())
