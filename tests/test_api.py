import json
import tempfile
import threading
import unittest
import urllib.request
import urllib.error
from pathlib import Path
from http.server import ThreadingHTTPServer
import server

class Workflow(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        server.DB = Path(cls.tmp.name) / 'test.db'
        with server.connection() as con:
            for name in ['Auditor líder','Outro auditor']:
                con.execute('INSERT INTO auditors(payload) VALUES (?)',(json.dumps(dict(name=name,qualifications={n:'leader' for n in server.NORMS})),))
            for norm in ['ISO 9001:2015','ANSN 1.16','ASME III']:
                con.execute('INSERT INTO requirements(payload) VALUES (?)',(json.dumps(dict(norm=norm,code='8.4',description='Requisito cadastrado')),))
        cls.http = ThreadingHTTPServer(('127.0.0.1', 0), server.Handler)
        threading.Thread(target=cls.http.serve_forever, daemon=True).start()
        cls.url = 'http://127.0.0.1:' + str(cls.http.server_port)

    @classmethod
    def tearDownClass(cls):
        cls.http.shutdown()
        cls.http.server_close()
        cls.tmp.cleanup()

    def request(self, path, body=None, method=None):
        req = urllib.request.Request(self.url+path, data=json.dumps(body).encode() if body else None, headers={'Content-Type':'application/json'}, method=method)
        with urllib.request.urlopen(req) as r:
            return json.load(r)

    def test_full_workflow_and_pac_validation(self):
        self.request('/api/areas', {'name':'Compras'})
        audit = dict(audit_type='I', year=2027, month=3, area='Compras', lead='Auditor líder', norms=['ISO 9001:2015'], requirements='ISO 9001:2015 — 8.4', lead_id=1, team_ids=[], requirement_ids=[1], status='Programada')
        aid = self.request('/api/audits',audit)['id']
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/audits/'+str(aid),dict(audit, report={'conclusion':'Prematuro'}),'PUT')
        audit['plan'] = dict(date='2027-03-10', lead='Outro auditor', team='Equipe', norms=['ANSN 1.16','ASME III'], requirements='ANSN 1.16; ASME III',lead_id=2,team_ids=[],requirement_ids=[2,3], confirmed=True)
        audit['status'] = 'Planejada'
        self.request('/api/audits/'+str(aid),audit,'PUT')
        self.assertEqual(self.request('/api/data')['audits'][0]['code'], 'AUDI-I-001-2027')
        finding = dict(audit_id=aid, kind='Não conformidade', area='Compras', description='Registro ausente', requirements='ISO 9001:2015 — 8.4', evidence='Amostra do processo de compra', pac='')
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
        self.assertEqual(state['findings'][0]['pac'],'PAC-001')
        self.assertEqual(state['findings'][0]['audit_id'],aid)
        with server.connection() as con:
            self.assertEqual(con.execute('SELECT count(*) FROM findings').fetchone()[0],1)

    def test_missing_audit_rejected(self):
        with self.assertRaises(urllib.error.HTTPError) as ctx:
            self.request('/api/findings',dict(audit_id=99999,kind='Observação',area='SMS',description='Verificar registro',requirements='ISO 14001',evidence='Registro analisado'))
        self.assertEqual(ctx.exception.code,400)

    def test_sequences_and_registered_areas(self):
        self.request('/api/areas', {'name':'Qualidade'})
        audit = dict(audit_type='I',year=2030,month=5,area='Qualidade',lead='Auditor',norms=['ASME III'],requirements='Requisito',lead_id=1,team_ids=[],requirement_ids=[3],status='Programada')
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
        req = self.request('/api/requirements',dict(norm='ISO 14001',code='6.1',description='Aspectos ambientais'))
        for payload in [dict(name='Sem habilitação',qualifications={}),dict(name='Inválido',qualifications={'ASME III':'inventado'})]:
            with self.assertRaises(urllib.error.HTTPError):
                self.request('/api/auditors',payload)
        with self.assertRaises(urllib.error.HTTPError):
            self.request('/api/requirements',dict(norm='ISO 14001',code='6.1',description='Duplicado'))
        self.request('/api/areas',{'name':'SMS'})
        audit=dict(audit_type='I',year=2032,month=6,area='SMS',lead='Auditor líder',requirements='Seleção',norms=['ISO 9001:2015'],lead_id=1,team_ids=[],requirement_ids=[1],status='Programada')
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
        req=self.request('/api/requirements',dict(norm='Secretaria Naval',code='N1',description='Requisito naval'))
        self.request('/api/requirements',dict(norm='Norma de teste',code='T1',description='Requisito adicional'))
        self.request('/api/areas',{'name':'Naval'})
        audit=dict(audit_type='I',year=2035,month=2,area='Naval',lead='Líder naval',requirements='N1',norms=['Secretaria Naval'],lead_id=leader['id'],team_ids=[auditor['id']],requirement_ids=[req['id']],status='Programada')
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

    def test_ui_is_served(self):
        with urllib.request.urlopen(self.url+'/') as r:
            self.assertIn('NUCLAUD',r.read().decode())
